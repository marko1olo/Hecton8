# LOG_MACRO_DATABASE_ARCHITECT

## 2026-05-13 - 100km B-Tree Pager

Status: PENDING VERIFICATION. Core tasks are checked or dependency-blocked. Whole-project compile is blocked outside the Macro Database domain.

What was wrong:
- The project had no isolated macro-scale pager for 100km persistence. Permanent RAM residency would not survive 4 million sectors on i3/MicroSD class hardware.
- There was no `IMacroDatabaseService` contract, no typed hydration signal, no `.h8db` fixed-page format, no vault-owned native payload cache, and no blackbox trail for pager faults.
- Direct singleton/database ownership would have coupled the system to bootstrap timing and other agents' work.

What was done:
- Added `IMacroDatabaseService`, `IMacroDatabaseNativeCacheOwner`, `IMacroDatabaseSignalSink`, `MacroDatabaseAup`, payload handles, stats, and telemetry contracts.
- Added `Hecton8.Core.Database` assembly with fixed 4096-byte `.h8db` header/root node format, page-local B-Tree SoA layout, memory-mapped file access, and unsafe page/payload reads.
- Registered MacroDB through `GlobalRegistry` and bootstrap composition without a singleton.
- Added `MacroDatabaseSignalBridge` and typed `SectorHydratedSignal` publication through `GlobalSignals`.
- Extended `GlobalDataVault` as the native owner for cached MacroDB payloads, including reserve/store/get/remove/key-copy/evict/stats APIs.
- Implemented AUP absolute coordinate hashing, Low/Middle/High/Ultra radius LODs, background hydration via `Awaitable`, >3km dehydration eviction with dirty append/update, offline repack, and 300-entry blackbox dump.
- Self-review fixed dirty pointer ownership and converted dirty appends from split `FileStream.Write` to mapped-pointer `UnsafeUtility.MemCpy`.
- Omega polish replaced hot sector division with reciprocal multiplication and scanned owned hot-path files for GC/math violations.

Cinematic Cheats used:
- Residency LOD instead of simulating the whole world: Low 1km, Middle 2km, High 3km, Ultra 4km.
- Append-only dirty writes instead of in-place rewrite churn.
- Offline defrag instead of runtime compaction.
- Identity-safe AUP sector hashing instead of Transform/floating-origin positions.
- B-Tree SoA page scans instead of managed object graphs.

Exact microseconds saved, estimated:
- Registry lookup over singleton/static discovery: 2-4 us predictable startup/service lookup path.
- Typed native hydration signal over string/UnityEvent path: 3-8 us per publication.
- MMF warm read over stream seek/read: 40-120 us per warm payload path depending on cache state.
- Unsafe node array reads over managed node/serializer traversal: 20-80 us per multi-node scan.
- Native payload cache over managed `byte[]` dictionary churn: prevents 50+ us allocation spikes and larger GC stalls under sector churn.
- Direct mapped dirty append over split stream writes: 5-15 us per dirty payload.
- Reciprocal sector math over runtime division: 1-5 us during large radius-window builds on low-end CPU.
- Low-tier 1km radius versus 2km middle cube: estimated 60-90% fewer page faults for weak devices.

Verification:
- Unity MCP `validate_script`: 0 diagnostics for `MacroDatabaseContracts.cs`, `H8MacroDatabaseFileFormat.cs`, `H8MacroDatabaseService.cs`, `GlobalDataVault.cs`, and `MacroDatabaseSignalBridge.cs`.
- `git diff --check` on owned touched files: pass, CRLF normalization warnings only.
- Full Unity compile: blocked by unrelated `Assets/_Project/Scripts/Audio/Echolocation/AcousticEcholocationRaymarch.cs` shadowing at lines 232-234 and missing `Hecton8.UI.Tools` Burst resolver dependency.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`: not authoritative because Unity has not regenerated the new database project and existing generated project references are stale/missing across unrelated domains.

Integrator notes:
- Keep `Hecton8.Core.Database` isolated from World/Memory implementations. It should see Contracts only; bootstrap wires the concrete vault/signal bridge.
- Do not mark this verified until the unrelated compile wall is cleared and Unity regenerates project files.
- Current status remains `PENDING VERIFICATION`, not master-grade, because evidence beats optimistic reporting.

## 2026-05-13 - Re-Audit / Async Safety Patch

Status: PENDING VERIFICATION. MacroDB-owned compiler fault found and fixed. Whole-project compile still blocked outside MacroDB.

What was wrong:
- `HydrateRadiusAsync` was documented in status but was missing from the interface/service.
- First implementation put `await` inside `H8MacroDatabaseService`, which is an unsafe class. Unity compile rejected it with `CS4004`.
- Dirty sectors were not force-flushed on service shutdown.
- Cached sector coordinates used `int3`, not 64-bit AUP-scale coordinates.
- Existing `.h8db` header validation only checked magic/version/root minimum and did not validate append/root bounds.

What was done:
- Added `HydrateRadiusAsync` to `IMacroDatabaseService`.
- Moved async awaits into non-unsafe `H8MacroDatabaseAsyncHydration`.
- Added locked background staging plus main-thread cache publication.
- Added `_dirtyPayloadKeys` and `FlushDirtyPayloadsLocked()` so shutdown appends dirty payloads before releasing the MMF pointer.
- Replaced cached sector coords with `SectorCoord64` and mixed full 64-bit sector components into hashes.
- Strengthened header validation for sector size, root page bounds/alignment, append bounds, and root key count.
- Added failed-initialization cleanup and locked blackbox dump.

Cinematic Cheats used:
- Background B-Tree traversal stages compact metadata only; it does not simulate or hydrate the whole world.
- Dirty shutdown flush preserves authored world truth without runtime defrag.
- Offline repack remains the fragmentation solution; no runtime stop-the-world compaction.

Exact microseconds saved, estimated:
- Async B-Tree traversal moves 30-80 us warm scans and cold page-walk stalls off the main thread.
- Dirty shutdown flush has no steady-frame cost; it prevents data loss at exit.
- 64-bit hash mixing is a negligible per-sector cost versus incorrect AUP identity.
- Header validation is cold open cost only, estimated 10-20 us.

Verification:
- Unity MCP `validate_script`: 0 diagnostics for owned MacroDB scripts after the patch.
- Unity compile: no MacroDB errors remain. Current errors are `EcosystemDirector` telemetry fields, duplicate `SuitHUDV4CanvasOverlay.OnGlobalRegistryServiceReplaced`, and missing `Hecton8.Vehicles.VFX` Burst dependency.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`: still red with 92 stale/generated reference errors; not authoritative for isolated Unity asmdef verification.

## 2026-05-13 - Loop 7 / Persistence Hardening

Status: PENDING VERIFICATION. MacroDB-owned scripts validate cleanly. Whole-project compile is still blocked outside MacroDB.

What was wrong:
- Sector coordinate tracking was caching every queried sector, including sectors with no payload. Over long travel this turns eviction metadata into travel history.
- Dirty append advanced the append cursor before B-Tree upsert success. A failed upsert could leave unreachable payload records at the file tail.
- File open/create and append/node allocation paths needed explicit max-file and signed-overflow guards before remapping.

What was done:
- Added a sector-coordinate scratch lane beside the sector hash query window.
- Cache sector coordinates only when payloads are actually cached/hydrated; remove cached coordinates when payloads evict.
- Added append cursor rollback if dirty payload append succeeds but B-Tree `UpsertPayloadOffset` fails.
- Added existing-file and new-file `MaxFileBytes` rejection before mapping.
- Added append and node allocation overflow checks before `EnsureMappedLength`.
- Re-ran the batch prompt extraction from `CURRENT_BATCH.md`; task count remains 18.

Cinematic Cheats used:
- Residency metadata now follows hydrated payloads only; empty queried space is treated as visual/query intent, not persistent state.
- Append-only writes remain the cheap runtime path; offline repack remains the cleanup path.
- Failure rollback avoids runtime compaction while keeping the tail clean enough for long sessions.

Exact microseconds saved, estimated:
- Coordinate-map pollution fix avoids rare native hash-map pressure and eviction stalls during long travel; steady-frame cost remains unchanged.
- Dirty append rollback saves future repack/search work by avoiding dead tail records after failed upsert.
- File and overflow guards are cold-path checks; expected steady-frame cost is 0 us.
- Previous MMF/unsafe/native-cache estimates remain unchanged: 40-120 us warm read improvement, 20-80 us traversal improvement, 5-15 us dirty append improvement.

Verification:
- Unity MCP `validate_script`: 0 diagnostics for `MacroDatabaseContracts.cs`, `H8MacroDatabaseFileFormat.cs`, `H8MacroDatabaseService.cs`, `GlobalDataVault.cs`, and `MacroDatabaseSignalBridge.cs`.
- Hot-path scan: no `foreach`, `.ToString(`, `string.Format`, string interpolation, LINQ markers, `math.sqrt`, or `math.normalize` hits in owned MacroDB files.
- Unity refresh/compile: no MacroDB errors and no current `GlobalDataVault` errors. Current blockers are external: `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs` missing `IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot, object, object)` and Burst resolver failure from `Hecton8.Vehicles.VFX` referencing unresolved `Hecton8.Core`.

Integrator notes:
- Do not widen `Hecton8.Core.Database` references. It must stay Contracts-only.
- The next compile owner must fix the visual hot-swap implementation and the Vehicles VFX Burst assembly reference before MacroDB can be marked verified.

## 2026-05-13 - Loop 8 / Dirty Eviction Safety

Status: PENDING VERIFICATION. MacroDB-owned scripts validate cleanly. Whole-project compile is still blocked outside MacroDB.

What was wrong:
- Dirty eviction had a use-after-free path: if disk append/upsert failed, the sector still entered the eviction batch. The vault could free the cached payload while `_dirtyPayloads` still held the pointer for shutdown flush.
- Sector coords were removed for every requested eviction, not only confirmed removals.
- Corrupt B-Tree child nodes could expose invalid key counts, and several offset checks used additive bounds that could wrap on corrupt files.

What was done:
- Dirty sectors now remain resident when append/upsert fails.
- Sector coords are removed only after the cache owner no longer reports the payload.
- B-Tree copy/find/update/insert paths fail closed on invalid key counts.
- Payload pointer, node pointer, header, mapping, and file alignment checks now avoid signed overflow.

Cinematic Cheats used:
- The runtime still uses append-only persistence with offline repack. No runtime journal/transaction layer was added.
- Failure policy favors keeping dirty sectors resident over forced eviction. Memory pressure is cheaper than data loss.

Exact microseconds saved, estimated:
- Steady-frame gain is 0 us; this is failure-path correctness.
- Eviction adds one native cache lookup per candidate after batch eviction, estimated sub-microsecond to low-single-digit microseconds depending on cache load.
- Avoided cost is catastrophic failure: freed dirty pointer, corrupt shutdown flush, and lost sector state.

Verification:
- Unity MCP `validate_script`: 0 diagnostics for `MacroDatabaseContracts.cs`, `H8MacroDatabaseFileFormat.cs`, `H8MacroDatabaseService.cs`, `GlobalDataVault.cs`, and `MacroDatabaseSignalBridge.cs`.
- Hot-path scan: no `foreach`, `.ToString(`, `string.Format`, string interpolation, LINQ markers, `math.sqrt`, or `math.normalize` hits in owned MacroDB files.
- `git diff --check`: pass, CRLF normalization warnings only.
- Unity refresh/compile: no MacroDB-owned errors. Current blockers are external: duplicate `CacheRuntimeDependencies`, `TryRegisterHotSwapListener`, `TryUnregisterHotSwapListener`, and `OnGlobalRegistryServiceReplaced` in `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`, plus Burst resolver failure for missing `Hecton8.Prologue.Space`.

Regression model:
- CPU: unchanged in hydration hot path; eviction has one extra native cache lookup per candidate.
- GC: static scan shows no managed allocation markers in owned hot paths; measured GC proof absent because project compile is externally blocked.
- Memory: dirty sectors can remain resident when disk append fails; this is intentional retention to prevent data loss.
- Correctness: dirty data now survives failed append/upsert instead of being freed behind the dirty map.
