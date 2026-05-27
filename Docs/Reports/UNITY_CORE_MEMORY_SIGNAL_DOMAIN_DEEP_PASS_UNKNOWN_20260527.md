# Unity Core Memory Signal Domain Deep Pass - UNKNOWN - 2026-05-27

Evidence class: static source, scoped diff checks, guarded SignalBus CLI build, and full SignalBus audit recheck.
No Unity import, Play Mode, profiler, GC capture, player build, or full-solution compile proof is claimed.
Full project compile errors were intentionally not fixed by user instruction.

## What Was Wrong

- `ModEventProjectionBridge` kept its cull telemetry blackbox as a local persistent
  `NativeArray` even though the production route can use `GlobalDataVault`.
- `TBDRPipelineSurgeonRuntime` opened multiple GlobalDataVault generation handles
  but did not release those handles on runtime dispose.
- `TBDRVertexBudgetVault` opened GlobalDataVault telemetry/counter buffers and reset
  local views, but the release route did not pass the owning `IDataVault` into dispose.
- `SignalBusContractAuditCli` treated `NativeMemoryBridgeLifetime.Session` registered
  rings as non-Vault warnings even when the ring is bounded, owner-local, and dump-only.

## What Was Done

- Added `BufferID.ShinobuModProjectionCullTelemetryRing = 70921`.
- Moved `ModEventProjectionBridge._cullTelemetry` production ownership to
  `IDataVault.EnsureGenerationHandle(...)` and `ReleaseBuffer(...)`.
- Kept a local sentinel fallback for `ModEventProjectionBridge` only when DataVault is
  unavailable during bootstrap or failure handling.
- Added explicit release of TBDR runtime DataVault handles in
  `TBDRPipelineSurgeonRuntime.ReleaseVaultBuffers()`.
- Added `TBDRVertexBudgetVault.Dispose(IDataVault)` so Vault-owned telemetry/counter
  buffers are released before local views are reset.
- Updated the SignalBus audit classifier so `NativeMemoryBridgeLifetime` is accepted as
  a bounded owner-local telemetry lifetime.

## Proof

- Baseline recheck before fixes:
  `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_DEEP_DOMAIN_RECHECK.json`.
  It reported `errors=0`, `confirmedErrors=0`, `warnings=148`, `infos=1169`.
- Post-source prebuild recheck with the already-built audit binary:
  `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_DEEP_DOMAIN_RECHECK_PREBUILD.json`.
  It reported `errors=0`, `confirmedErrors=0`, `warnings=146`, `infos=1171`.
- Ownership movement in that prebuild audit:
  `LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT` moved from `3` to `1`.
- New Vault aliases in that prebuild audit:
  `LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS` moved from `3` to `5`.
- Guarded CLI build:
  `BUILD_UNKNOWN_SIGNAL_CLI_DEEP_DOMAIN_RECHECK_20260527.log`.
  It reports `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Final audit from the rebuilt CLI:
  `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_DEEP_DOMAIN_RECHECK_FINAL.json`.
  It reported `errors=0`, `confirmedErrors=0`, `warnings=145`, `infos=1172`.
- Final ownership movement:
  `LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT=0`,
  `LOCAL_NATIVE_TELEMETRY_RING_OWNER_LOCAL=8`,
  `LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS=5`.
- Scoped `git diff --check` on touched files passed with line-ending warnings only.
- Brace-balance spot checks returned `0` deltas for:
  `H8Memory.cs`, `ModEventProjectionBridge.cs`,
  `TBDRPipelineSurgeonRuntime.cs`, and `TBDRPipelineSurgeonTypes.cs`.
- Documentation gates after report update:
  `VerifyDocStructure.py pass=true activeDocCount=694 encodingWithoutUtf8Sig=0`;
  `OOP_Doc_Scanner.py finalPass=true activeFileCount=694 sourceSyncPass=true`.

## Residual Debt

- Full `Hecton8.slnx` compile was not launched and full project compile errors were
  not fixed by explicit user instruction.
- `RUNTIME_SYNC_FILE_IO_REVIEW=65` remains. Representative core hits were inspected
  and are cold persistence, setup, replay, or fault/dump routes; no hot-path rewrite
  was made without profiler or owner proof.
- `SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW=69` remains mostly outside this core/memory
  pass and requires graphics/UI owner review.
- `DUPLICATE_SIGNAL_LIKE_NAME_REVIEW=8` remains across atmosphere, ocean/fluid,
  structural, and thermal owners. At least one involved source file is dirty under
  concurrent work, so a blind rename would be cross-domain churn.
- `EDITOR_MANAGED_STRING_IN_SIGNAL_REVIEW=3` remains editor diagnostics only in this
  pass and was not treated as runtime payload debt.

Runtime microseconds saved: `0` claimed. This pass fixes ownership/lifetime correctness
and audit precision; no profiler/player measurement was run.
