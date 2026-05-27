# Unity Signal Telemetry Ownership Recheck - UNKNOWN - 2026-05-27

Status: STATIC_AUDIT_VERIFIED_BUILD_FAILED_DOC_GATES_GREEN

## Scope

Domain: Core and memory infrastructure, SignalBus contracts, zero-GC runtime architecture.

Touched files:

- `Assets/_Project/Scripts/UI/FontStreamingManager.cs`
- `Tools/SignalBusContractAuditCli/Program.cs`
- `Docs/Tasks/Status_UNKNOWN.md`
- `Docs/AgentLogs/Rationale_UNKNOWN.md`
- `Docs/AgentLogs/LOG_UNKNOWN.md`
- `Docs/Reports/README.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`

## Problems Found

- `FontStreamingManager.ProcessSwapBatch()` resolved `_targetFont.material` during swap drain ticks.
- The contract audit did not recognize `VaultGenerationHandle` NativeArray aliases as GlobalDataVault-owned.
- The contract audit did not fully recognize helper-owned `Allocate*Array` plus register/dispose telemetry rings.

## Changes

- Cached the TMP material in `BeginSwapQueue()` and reused it in `ProcessSwapBatch()`.
- Cleared the cached material in `ResetSwapState()`.
- Added Vault generation alias detection to `SignalBusContractAuditCli.GetOwnership()`.
- Added helper allocation/register/unregister/dispose detection for NativeArray telemetry fields.
- Added detection for bare `AllocateArray<T>()` helper assignments.
- Added detection for `ref _fieldHandle` Vault alias routes that assign `_field = resolved`.

## Non-Claims

- Runtime microseconds saved: `0`.
- No profiler, player, Play Mode, or GC proof has run in this pass.
- No claim is made that all SRP material warnings or NativeArray telemetry warnings are fixed.
- Full solution build is not green.

## Verification

CLI build proof:

- `BUILD_UNKNOWN_SIGNAL_CLI_TELEMETRY_OWNERSHIP_RECHECK_20260527.log`: exit `0`, `0` warnings, `0` errors.
- `BUILD_UNKNOWN_SIGNAL_CLI_TELEMETRY_OWNERSHIP_RECHECK2_20260527.log`: exit `0`, `0` warnings, `0` errors.

Final audit proof:

- `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_TELEMETRY_OWNERSHIP_RECHECK2.json`
- Files: `2441` C# / `71` compute
- Errors: `0`
- Confirmed errors: `0`
- Warnings: `166`
- Infos: `832`
- `SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW`: `72`
- `LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY`: `1`
- `LOCAL_NATIVE_TELEMETRY_RING_OWNER_LOCAL`: `3`
- `LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS`: `3`

Delta against `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_EXEC_CARRIER_RECHECK.json`:

- Total warnings: `171 -> 166`
- Infos: `826 -> 832`
- SRP material warnings: `73 -> 72`
- Declared-only telemetry rings: `5 -> 1`
- Registered telemetry rings: `10 -> 11`
- `CombatDamageRuntime._telemetryRing`: warning -> owner-local info
- `LocRegistry._telemetryFrames`: warning -> Vault alias info

## Full Solution Build Boundary

Command:

- `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`

Proof:

- Log: `BUILD_UNKNOWN_TELEMETRY_OWNERSHIP_FULL_SOLUTION_RECHECK_20260527.log`
- Guard at launch: CPU `8`, compiler process count `0`
- Tool boundary: timed out after `604s`; build process later ended.
- Error lines in log: `350`
- Warning lines in log: `0`
- Touched source file errors: `0`

Largest project buckets:

- `Hecton8.Core.csproj`: `132`
- `MeshBakerCore.csproj`: `70`
- `Assembly-CSharp-Editor.csproj`: `69`
- `MapMagic.csproj`: `21`
- `AstarPathfindingProject.csproj`: `21`

Representative failure class:

- MapMagic duplicate/ambiguous editor API and missing generated/runtime types.
- MeshBaker missing `MB2_*`, `MB3_*`, texture combiner, and atlas types.
- NiceVibrations editor importer missing Unity editor importer types.
- Several generated/project-reference assemblies are unresolved.

This is a project graph/plugin dependency boundary, not proof that this pass compiles cleanly or fails in touched source.

## Remaining Debt

- Dirty SRP/material routes remain outside this pass.
- Dirty or authoritative duplicate signal-like families remain owner-domain work.
- Dirty `WfcOutpostPowerBootRuntime._blackBox` is the only remaining declared-only telemetry warning.
- Full solution build remains blocked by generated/plugin project graph errors.

## Documentation Gates

- `VerifyDocStructure.py`: `pass=true`, `activeDocCount=688`, `encodingWithoutUtf8Sig=0`
- `OOP_Doc_Scanner.py`: `finalPass=true`, `activeFileCount=688`, `sourceSyncPass=true`
- Word reduction: `34.132669128506585%`
- Extra doc-gate fix: split long route-card paragraphs in `PROLOGUE_ORBIT_HANDOFF_ROUTE_CARD_13PRO.md` and `TERRAIN_RUNTIME_AUTHORITY_ROUTE.md` without changing technical claims.
