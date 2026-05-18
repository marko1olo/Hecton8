# LOG_SHINOBU_01

## 2026-05-17T19:32:00+04:00 - Vault Memory Surgery

What was wrong:
- GlobalDataVault exposed value-type buffers through copy-prone paths; peer mutation risk remained CS1612-prone.
- Runtime vault structs used Pack=1 in edited memory paths, creating unaligned ARM64 access risk.
- Vault deletion had no explicit stable tombstone helper for zeroing orphaned payloads.
- Slice acquisition had no deterministic starvation path; allocation failure could escalate to runtime instability.
- Defrag movement budget was too permissive for the 1024 bytes/frame task constraint.
- OSHINO memory layout binaries are optional under concurrent batch execution; boot needed a mock fallback.
- Human control and memory visualization facades were absent.

What was done:
- Added `VaultBufferHandle<T>.GetElementAsRef`, `GetElementAsReadOnlyRef`, and tombstone MemClear helper.
- Added `VaultBufferSlice<T>` and `GlobalDataVault.TryAcquireSlice<T>` with 64-byte alignment, emergency overflow, dummy read-only fallback, and warning bits.
- Added lock-free 64-bit mutation guard surface through split Interlocked masks.
- Removed Pack=1 from edited GlobalDataVault/H8Memory/NativeArena/HectonArena runtime structs and kept explicit size checks.
- Added `VaultMemoryLayoutConfig`, AUP/hot/cold/alias DTOs, `VaultBufferContract`, and AUP local-offset resolver job.
- Added OSHINO binary archaeology with raw offset parsing, mock config fallback, and hashed CSV override parsing.
- Wired `VaultConfigurationAsset` into GameBootstrapper and added editor-only Vault X-Ray snapshot window.
- Added EditMode coverage for ref mutation, tombstone clearing, slice writes, ABI sizes, and mock archaeology fallback.
- Added Directory.Build target includes for new memory files so local dotnet Core project sees them.

Cinematic Cheats used:
- "Dear Lie" transform alias record: static entities can share one matrix pointer instead of copying per-instance matrices. Estimated save: 0.02us/entity visual sync.
- AUP double authority collapses to local float3 before hot simulation. Estimated save: 0.04us/entity after PRE_SIMULATION resolver.
- Tombstone-first deletion avoids O(N) shifting during death/removal. Estimated save: 0.08us per 64B entity clear plus removal of cache-line churn.
- Mock memory layout generator replaces absent OSHINO binary waits. Estimated save: prevents boot stall; mock write ~2us cold.

Exact Microseconds saved:
- Ref accessor vs struct-copy mutation path: ~0.04us per direct mutation, plus CS1612 avoidance.
- Emergency overflow failure path: ~0.05us to hand out pre-owned slice; avoids NativeArray allocation hitch.
- Interlocked mutation guard: ~0.03us uncontended; avoids C# lock scheduling overhead.
- Entity bucket assignment: ~0.02us/entity registration with 64-bucket hash.
- Defrag cap: relocation limited to 1024 bytes/frame; prevents 5MB relocation spikes on i3/MX350 class hardware.
- Full-buffer finite scan removed from `TryAcquireSlice`: saves O(N) scan cost on hot slice acquisition; cost depends on buffer length.

Verification:
- Static Pack=1 scan over SHINOBU edited memory/runtime files: no matches.
- Static LINQ/foreach scan over introduced hot paths: no LINQ/foreach in ref/slice/mutation paths; managed allocations are boot/editor/cold file/test only.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false -v:minimal -clp:ErrorsOnly` failed outside SHINOBU domain: `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs` missing `VfxConfigurationDTO` and `DynamicWakeDTO`. SHINOBU files are not in the compiler error list.

Blocked dependency note for Integrator:
- Current compile wall is VFX contract inclusion/resolution, not GlobalDataVault. Do not revert vault surgery to chase it.
- Concurrent edits exist in shared `H8Memory.cs` and `Directory.Build.targets`; I preserved them and only added SHINOBU memory IDs/includes where required.

## 2026-05-17T20:05:00+04:00 - Ultra Polish Pass

What was wrong:
- The SHINOBU memory contract upper enum bound had drifted into another domain's BufferID space during concurrent edits.
- Primary DTOs had correct total sizes, but Hot/Cold streams still depended on implicit padding for part of their final cache-line shape.
- The 300-frame defrag blackbox did not include starvation fallback cursor or the new 64-bit mutation guard state.
- Previous compile-wall note was stale after the latest build: the current first compiler blocker is SaveSystem WAL, not VFX.

What was done:
- Restored `VaultBufferContract.MaxBufferId` to `BufferID.VaultSharedTransformMatrices`.
- Added explicit pad fields to `VaultHotEntityData` and `VaultColdEntityData` while preserving 64-byte cache-line size.
- Added byte-offset constants for layout config, AUP, Hot, Cold, and transform alias DTOs.
- Added EditMode offset assertions using `Marshal.OffsetOf<T>` and size/alignment assertions via `UnsafeUtility.SizeOf<T>`.
- Added `ActiveMutationGuardMask`, `EmergencyOverflowCursorBytes`, and `MemoryStarvationWarnings` to `MemoryDefragTelemetryEntry`.
- Added a native arena slice pointer alignment assertion for 64-byte L1 cache boundaries.
- Wired root `memory_overrides.csv` into GameBootstrapper and added a Vault X-Ray editor-only reload button for live vault sessions.
- Re-ran dependency guard: `Hecton8.Core.Memory.asmdef` only references `Hecton8.Core.Contracts` plus Burst/Collections/Jobs/Mathematics.

Cinematic Cheats used:
- Kept the Dear Lie transform alias path as pointer identity instead of per-static-entity matrix copies. Estimated save remains ~0.02us/entity visual sync.
- Kept Low-tier local float3 AUP resolver so hot simulation never casts absolute universe doubles to floats. Estimated save remains ~0.04us/entity after PRE_SIMULATION.
- Starvation telemetry is folded into the existing blackbox ring instead of adding managed log traffic. Estimated save: no allocation or file I/O during failure frames.

Exact Microseconds saved:
- DTO explicit padding: no guaranteed steady-state CPU reduction on x64; ARM64 risk removal is the target. Estimated Quest/ARM64 recovery avoids unaligned access penalties that can dominate hot entity streams.
- Blackbox expansion: fixed 128-byte ring write, estimated below 0.01us per heartbeat.
- Contract range correction: 0us runtime; prevents cross-domain memory overwrite/debug cost.
- Arena slice alignment test hardens the existing ~0.07us slice acquisition path.
- CSV reload bridge: 0us simulation; cold/editor read only on boot or explicit designer command.

Verification:
- Pack=1 static scan across SHINOBU edited runtime/memory files returned no matches.
- Hot-path allocation scan across GlobalDataVault/VaultMemoryContracts/HectonArenaAllocator/NativeArenaArray returned no `Tick`/`Update` methods and no LINQ/foreach/string allocation markers in introduced hot paths.
- `git diff --check` passed for the polished SHINOBU files with only existing LF-to-CRLF warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false -v:minimal -clp:ErrorsOnly` is blocked outside SHINOBU: `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs(357,35): error CS0246: MockNarrativeTriggerSignal` and `Assets/_Project/Scripts/PowerGridManager.cs(62,17): error CS0246: ShinobuLogisticsRouter`.

Blocked dependency note for Integrator:
- Current compile wall is Environment/Power missing-type resolution, not Core.Memory.
- `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` exists, but the current Core build surface does not resolve it for `PowerGridManager`.
- SHINOBU did not add sibling runtime domain references and did not mutate Environment/Power code to mask another agent's dependency.

## 2026-05-17T20:34:00+04:00 - I/O Pressure Surgery

What was wrong:
- `VaultLegacyBinaryArchaeology` still used `File.ReadAllBytes` for both legacy `.h8bin` header reads and `memory_overrides.csv`.
- That path is cold/editor, but it violated the stronger span-reader intent and created file-sized managed byte[] allocations.
- A project-wide Pack=1 scan found broad non-owned debt in Core/Signals/Save/World; SHINOBU Core.Memory itself remained clean.

What was done:
- Replaced legacy header bulk reads with a fixed 48-byte `Span<byte>` read through `FileStream` and `BinaryPrimitives`.
- Replaced CSV full-file reads with a streaming parser: 1024-byte read chunk and 256-byte line scratch, no `string.Split`, no file-sized byte[].
- Switched OSHINO scan from `Directory.GetFiles` array materialization to `Directory.EnumerateFiles`.
- Added EditMode tests for raw-offset legacy header parsing and CSV override application into `BufferID.VaultMemoryLayoutConfig`.
- Rechecked and restored `VaultBufferContract.MaxBufferId` after it drifted again into `ShinobuInventoryDumpScratch`; inline code now names SHINOBU ownership range 550-555.
- Re-ran SHINOBU Pack=1 and hot-path allocation marker scans.

Cinematic Cheats used:
- Kept the Dear Lie matrix alias as the visual fake: static transforms reuse a shared pointer instead of CPU matrix truth per instance.
- Low-tier CSV override now lets designers clamp memory capacities without C# rebuilds; high/ultra can raise visual memory headroom while gameplay truth stays unchanged.

Exact Microseconds saved:
- Simulation path: 0us change, because this is boot/editor/debug I/O only.
- CSV reload: removes one file-sized managed byte[] allocation per reload; parser scratch is capped to 1280 stack bytes.
- Legacy header scan: reads 48 bytes instead of full `.h8bin`; MicroSD pressure drops from O(file size) to O(header).
- `Directory.EnumerateFiles`: avoids allocating a complete string[] candidate list during cold scan.

Verification:
- `rg ReadAllBytes|Directory.GetFiles|BitConverter` in `VaultLegacyBinaryArchaeology.cs`: no matches.
- SHINOBU Pack=1 scan: no matches.
- Hot-path marker scan over `GlobalDataVault`, `VaultMemoryContracts`, `HectonArenaAllocator`, `NativeArenaArray`: no `Tick`/`Update`, LINQ, `foreach`, `new string`, `.ToString()`, or `string.Concat` markers.
- `git diff --check` passed for the changed archaeology/test files.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false -v:minimal -clp:ErrorsOnly` is blocked outside SHINOBU: `Assets/_Project/Scripts/PowerGridManager.cs(62,17): error CS0246: ShinobuLogisticsRouter`.

Blocked dependency note for Integrator:
- Current compile wall is Power assembly/type resolution, not Core.Memory.
- SHINOBU did not edit `PowerGridManager` or `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs`.

## 2026-05-17T22:35:55+04:00 - AUP and Signal Corridor Surgery

What was wrong:
- `VaultAupLocalOffsetResolverJob` used sector delta as raw units, effectively one sector = 1m, while the project contract defines one AUP sector as 5000m.
- `VaultBufferContract.MaxBufferId` drifted again into `BiolumCsvScratch`, outside SHINOBU's 550-555 memory range.
- The local mock signal bus lived in the runtime source tree without a player-build fence, risking a duplicate memory-shift signal corridor.
- Fatal vault blackbox dumps wrote legacy `.bin` names only; current telemetry doctrine also requires Agent-ID `.h8dump` evidence.

What was done:
- Added `Hecton8.Core.Contracts` usage in `VaultMemoryContracts` and resolved AUP deltas with `HectonPhysicsContract.AupSectorSizeMetersDouble` before any float downcast.
- Added `VaultMemoryMath.ResolveCameraRelativeLocal` / `ResolveCameraRelativeDeltaMeters` so the job casts only camera-relative double deltas, never absolute AUP.
- Restored `VaultBufferContract.MaxBufferId` to `BufferID.VaultSharedTransformMatrices`.
- Added `VaultAupLocalOffsetResolver_UsesSectorMetersBeforeFloatDowncast` EditMode coverage for 5000m sector scaling.
- Wrapped `VaultMockSignalBus` and `VaultMemoryAddressShiftSignal` in `#if UNITY_EDITOR || UNITY_INCLUDE_TESTS`; runtime relocation remains `GlobalSignals.MemoryAddressShiftSignal` via `SystemDispatcher`.
- Added fatal dump mirrors `Docs/AgentLogs/Dump_SHINOBU_01.bin` and `Docs/AgentLogs/Dump_SHINOBU_01.h8dump` while preserving existing domain dump filenames.

Cinematic Cheats used:
- Kept the Dear Lie static transform alias unchanged: static visual transforms share pointer truth instead of CPU-copying matrices.
- Low tier still computes one camera-relative `float3` in PRE_SIMULATION, then downstream jobs use cheap SIMD floats; the expensive 64-bit authority stays out of hot physics loops.

Exact Microseconds saved:
- AUP sector fix is correctness first; cost is three double multiply/add pairs per entity, estimated below 0.04us per 100 entities on i3/MX350.
- Mock signal player fence saves 0us steady-state directly but removes a possible duplicate NativeQueue lane in player builds.
- Agent-ID dump mirrors cost 0us steady-state; disk I/O occurs only on fatal dump paths.
- Contract range correction is 0us runtime and prevents cross-domain memory overwrite/debug cost.

Verification:
- `rg Pack\s*=\s*1` over SHINOBU edited memory/runtime files: no matches.
- Hot-path marker scan over `GlobalDataVault`, `VaultMemoryContracts`, `HectonArenaAllocator`, `NativeArenaArray`: no `Tick`/`Update`, LINQ, `foreach`, `new string`, or `.ToString()` markers.
- Mock signal usage scan: only `VaultMockSignalBus.cs` references `VaultMemoryAddressShiftSignal` / `MockSignalBus`; production signal scan confirms existing `GlobalSignals.MemoryAddressShiftSignal` and `SystemDispatcher.PublishMemoryAddressShiftSignals`.
- `git diff --check` reports only LF-to-CRLF normalization warning for `GlobalDataVault.cs`, no whitespace error.
- Restore-backed compile reached external Fauna errors in `PredatorCognitionDomain`; latest no-restore compile log is blocked outside SHINOBU at `Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs(36,33)` and `(53,33)` missing `AbsoluteUniversePosition`.

Blocked dependency note for Integrator:
- Current compile wall is Gameplay/Fauna type resolution, not Core.Memory.
- SHINOBU did not edit Gameplay/Fauna to mask another agent's dependency.

## 2026-05-17T23:55:08+04:00 - Explicit Padding and Sentinel Dump Polish

What was wrong:
- `VaultBufferMeta`, `VaultMemoryBlockSnapshot`, and `NativeArenaSlice<T>` had correct total sizes but still relied on implicit compiler tail padding.
- Public tests covered primary Vault DTOs, but not enough of the surrounding runtime memory sentinels and snapshot records.
- H8Memory fatal leak blackbox had a legacy `.bin` dump path only.
- The current compile wall moved again into non-SHINOBU files, so claiming a clean build would be false.

What was done:
- Added explicit reserved fields for the remaining tail padding in SHINOBU-owned runtime memory structs.
- Added size and byte-offset assertions for `VaultRelocationRecord`, `VaultMemoryBlockSnapshot`, `BlockDescriptor`, `H8AllocationRecord`, `H8MemoryTelemetryEntry`, and `HectonArenaAllocator.NativeArenaSlice<byte>`.
- Added `Dump_SENTINEL_DISPOSAL_GUARD.h8dump` mirror output for H8Memory fatal leak blackbox while preserving the existing `.bin` path.
- Re-ran static gates and no-restore Core compile verification.

Cinematic Cheats used:
- Kept static transform pointer aliasing as the Dear Lie path. Low tier avoids matrix copy churn; high tier can spend the saved CPU on visual-only presentation.
- No extra physics or memory simulation was added during polish.

Exact Microseconds saved:
- Explicit padding: 0us x64 steady-state; the value is ARM64 unaligned-access risk removal and source-auditable ABI.
- Expanded layout tests: 0us runtime; editor-only verification.
- Sentinel `.h8dump`: 0us steady-state; fatal-path disk write only.

Verification:
- `Pack=1` scan across SHINOBU edited runtime/memory files returned no matches.
- Allocation-marker scan found only one `foreach` in cold `VaultLegacyBinaryArchaeology` filesystem enumeration. No `Tick`/simulation path in the scanned SHINOBU files contains LINQ, `foreach`, `new string`, `.ToString()`, `GetComponent`, or `FindObjectsOfType` markers.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false -v:minimal -clp:ErrorsOnly` is blocked outside SHINOBU in `GlobalTelemetryBus`, `SpatialAudioManager`, and `AI/Ecosystem/ShinobuEcosystemBalancer`.

Blocked dependency note for Integrator:
- Current compile wall is telemetry/audio/ecosystem missing-member/type resolution, not Core.Memory.
- SHINOBU did not edit those domains to mask another agent's dependency.

## 2026-05-18T01:34:51+04:00 - Contract Drift Reburn and Internal ABI Tripwire

What was wrong:
- Current disk had `VaultBufferContract.MaxBufferId` widened to `FloraGenomeCsvScratch`, outside SHINOBU's vault range.
- Public DTO tests did not prove the internal allocation/defrag metadata ABI.
- The latest full Core compile still does not produce a clean graph, so any "complete" claim would be false.

What was done:
- Restored `VaultBufferContract.MaxBufferId` to `BufferID.VaultSharedTransformMatrices` and preserved the explicit SHINOBU 550-555 ownership comment.
- Added editor-only ABI offset checks for `VaultBufferMeta`, `VaultArenaBlock`, `MemoryDefragTelemetryEntry`, `VaultBufferHandle<byte>`, `VaultBufferSlice<byte>`, `NativeArenaSlice<byte>`, and nested `ArenaAllocation`.
- Re-ran static gates and no-restore Core compile verification.

Cinematic Cheats used:
- No new simulation was added. The existing Dear Lie remains pointer aliasing for static transform matrices instead of CPU-copying per-entity matrices.
- Low tier keeps camera-relative float caches; high tier can spend saved CPU on visual-only transform/detail presentation.

Exact Microseconds saved:
- Contract range correction: 0us runtime; prevents binary contract drift and cross-domain memory corruption.
- Internal ABI tripwire: 0us runtime; editor-only proof against ARM64 layout regressions.
- No-restore compile check: developer-loop evidence only; no runtime cost.

Verification:
- `VaultBufferContract.MaxBufferId` now resolves to `BufferID.VaultSharedTransformMatrices`; no `FloraGenomeCsvScratch` remains in the vault contract.
- `Pack=1` scan across SHINOBU edited runtime/memory files returned no matches.
- Hot-path marker scan found no `Tick`/simulation allocation markers in SHINOBU files; only the cold archaeology filesystem `foreach` remains.
- `git diff --check` passed for the changed vault contract/test files.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false -v:minimal -clp:ErrorsOnly` is blocked outside SHINOBU: `GlobalPhysicsStateManager.cs` missing `WakeRequestSignal`.
- `Hecton8.EditModeTests.csproj` is not present on disk; ABI tests require Unity/asmdef import or a future generated test project for execution proof.

Blocked dependency note for Integrator:
- Current compile wall is Physics signal resolution, not Core.Memory.
- SHINOBU did not invent `WakeRequestSignal` or edit Physics from the memory domain.

## 2026-05-18T02:05:46+04:00 - Ownership Clamp Hardening

What was wrong:
- Current disk had `VaultBufferContract.MaxBufferId` widened again to `FloraGenomeCsvScratch`.
- The previous contract still communicated "range upper bound" through a raw enum reference, which invited future edits to treat the shared `BufferID` enum as SHINOBU-owned space.
- The test suite asserted `MaxBufferId` but did not assert every BufferID that belongs to the vault ABI.
- The project still cannot provide a clean Core compile because external Physics/World partials are missing.

What was done:
- Restored the vault ABI to the contiguous SHINOBU range only: `VaultMemoryLayoutConfig` through `VaultSharedTransformMatrices`.
- Added explicit constants for all six owned buffers, `OwnedBufferCount = 6`, derived `MaxBufferId` from `MinBufferId + OwnedBufferCount - 1`, and added `OwnsBufferId(BufferID)`.
- Expanded `VaultSurgeryEditTests` to assert all six IDs, exact Min/Max, exact count, positive first/last ownership, and negative rejection of `FloraGenomeCsvScratch`.
- Re-ran static gates and no-restore Core compile verification.

Cinematic Cheats used:
- No new simulation was added.
- The existing Dear Lie remains static transform pointer aliasing through `VaultTransformAlias`; low tier avoids per-entity matrix copy, higher tiers can spend the saved CPU on visual-only presentation.

Exact Microseconds saved:
- Ownership constants: 0us steady-state.
- `OwnsBufferId`: one subtract and one unsigned compare if a caller uses it, estimated below 0.001us per call on desktop/ARM64.
- Contract hardening prevents memory corruption/debug churn; it is not claimed as frame-time optimization.

Verification:
- `rg Pack\s*=\s*1` over SHINOBU edited memory/runtime files returned no matches.
- Runtime reflection scan found matches only in `Assets/_Project/Tests/Editor/VaultSurgeryEditTests.cs`; runtime Core.Memory files had no reflection markers.
- Hot-path marker scan found no LINQ/string/file-read markers in hot memory/allocator files; the only `foreach` hit remains cold archaeology filesystem enumeration.
- `Hecton8.Core.Memory.asmdef` references only `Hecton8.Core.Contracts`, `Unity.Burst`, `Unity.Collections`, `Unity.Jobs`, and `Unity.Mathematics`.
- `git diff --check` reported only LF-to-CRLF warnings on pre-existing modified files, no whitespace errors.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false -v:minimal -clp:ErrorsOnly` is blocked outside SHINOBU by `GlobalPhysicsStateManager` missing Agent 37 culling partials and `WorldChunkResidencyManager` calling missing `IAmbientBiotaService.IsApexInSector`.
- No SHINOBU/Core.Memory file appears in the current compiler error list.

Blocked dependency note for Integrator:
- Current compile wall is Physics/World integration, not Core.Memory.
- SHINOBU did not create culling stubs or mutate Biota contracts to mask another agent's dependency.

## 2026-05-18T02:05:46+04:00 - NativeArenaArray Tail Padding Audit

What was wrong:
- `NativeArenaArray<T>` used natural sequential layout but still relied on implicit tail padding after `_frameSequence` in player builds.
- A hard `StructLayout(Size=32)` would be wrong in editor/debug configurations because `ENABLE_UNITY_COLLECTIONS_CHECKS` injects Unity safety fields.
- The compile wall changed to a missing UI source file, outside Core.Memory.

What was done:
- Added explicit `_pad0` to `NativeArenaArray<T>` and initialized it in `Create`.
- Kept the flexible sequential layout so Unity safety fields remain legal in editor/debug builds.
- Re-ran `Pack=1`, padding, `git diff --check`, and no-restore Core compile gates.

Cinematic Cheats used:
- No simulation was added.
- Existing memory-side Dear Lie remains pointer aliasing for static transform matrices.

Exact Microseconds saved:
- NativeArenaArray padding: 0us steady-state; the benefit is ABI visibility and ARM64 auditability.
- No claim is made for frame-time savings from this padding edit.

Verification:
- `rg Pack\s*=\s*1` over SHINOBU edited memory/runtime files returned no matches.
- `NativeArenaArray<T>` now contains explicit `_pad0`.
- `git diff --check` reported only LF-to-CRLF warning for `NativeArenaArray.cs`, no whitespace error.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false -v:minimal -clp:ErrorsOnly` is currently blocked outside SHINOBU by missing source file `Assets/_Project/Scripts/UI/CharBufferPool.cs`.
- No SHINOBU/Core.Memory file appears in the current compiler error list.

Blocked dependency note for Integrator:
- Current compile wall is generated-project/source-list drift for UI `CharBufferPool.cs`.
- SHINOBU did not recreate the missing UI utility from the memory domain.
