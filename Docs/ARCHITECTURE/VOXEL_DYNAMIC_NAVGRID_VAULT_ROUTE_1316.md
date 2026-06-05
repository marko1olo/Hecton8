# Voxel Dynamic NavGrid Vault Route - Agent 1316

Status: `STATIC_ROUTE_DOC / RUNTIME_PROOF_PENDING`
Evidence class: `STATIC_DOC / STATIC_SOURCE`
Owner domain: world streaming/voxel navigation
Review disposition: `YELLOW / STATIC_DOC_ONLY` until compile/import/runtime/profiler/player proof exists.

Owner: `SystemID.WorldStreaming`

Scope: `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs` and lifecycle binding in `VoxelDynamicNavGridRuntimeLifecycle.cs`.

## Buffer Contract

- Record lanes use `BufferID.VoxelDynamicNavGridRecordBufferBase` through `BufferID.VoxelDynamicNavGridRecordBufferEnd`.
- Range: `79000..82071`.
- Layout: `512` record slots, `6` lanes per record.
- Lanes per slot:
  - `0`: current passability byte grid.
  - `1`: next passability byte grid.
  - `2`: current base passability byte grid.
  - `3`: next base passability byte grid.
  - `4`: current clearance distance ushort grid.
  - `5`: next clearance distance ushort grid.
- Telemetry uses `BufferID.VoxelDynamicNavGridTelemetryRing=82072` and `BufferID.VoxelDynamicNavGridTelemetryCursor=82073`.
- Telemetry DTO: `NavGridTelemetryEntry`, explicit `Size=64`, 8-byte state hash first, 4-byte scalar lanes next, 2-byte failure/phase tail.

## Synchronization Contract

- Runtime records store only `VaultGenerationHandle<T>` descriptors for nav-grid data.
- Every read/write resolves phase-local DataVault views and releases write locks in `finally`.
- Lock paths check the compaction fence before lock acquisition and immediately after acquisition.
- No DataVault view or write lock is intentionally held across frames, yields, or dispatcher phase boundaries.
- The current rebuild/dilation path is synchronous owner-phase work by design.
  Worker-thread restore requires separate transient scratch plus commit-window design.
  Scheduling directly over Vault views is forbidden.
- Volume record identity is served by a cold fixed shell pool.
  Runtime registration reuses a prebuilt shell and shared bounded portal scratch.
  It does not allocate per-record portal arrays.

## Failure Contract

- Missing vault, active compaction, invalid BufferID, stale handle, lock contention, capacity mismatch, and budget overflow fail closed.
- Failure routes write numeric fault codes into the unmanaged telemetry ring when the ring is available.
- Pure-void volumes retain descriptor ownership until an explicit read-contract proves they can be represented without allocated nav-grid rows.

## Review Disposition

- Result: `YELLOW`.
- Static proof: `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_1316_WORLD_LOOP70.json` reports `0` forbidden persistent native candidates over `Assets/_Project/Scripts/World`.
- Hot-path proof: `Docs/Reports/VOXEL_RUNTIME_HOTPATH_AUDIT_1316_LOOP70.json` reports `0` hot allocation tokens.
  Its managed-risk list still includes static/cold constructors and struct constructors.
  The true runtime `GetOrCreateRecord` allocation was moved to `CreateRecordPool`.
- Blocker before `GREEN`: Unity-generated project files are absent.
  `dotnet build Hecton8.slnx --no-restore` stops on 62 missing `.csproj` files before source compilation.
  Runtime Profiler/GC proof is also still absent.
