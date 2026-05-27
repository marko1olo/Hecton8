# Unity Signal Layout/Alias Classifier Pass - UNKNOWN - 2026-05-27

Evidence class: static source/tooling, partial final verification.

## What Was Wrong

- `SignalBusContractAuditCli` checked `ConfigureCacheLineCritical<T>()` payload size only against structs declared in the same file as the configure call.
  This produced false stride debt for `ProgressionEventSignal` and `VocalCueSignal`, whose explicit layouts live in `Core/Signals/GlobalSignalPayloads.DomainRemainder.cs`.
- `TelemetryArrayRegex` treated expression-bodied `NativeArray<T>` accessors such as `_blackBox => ResolveBuffer(in _blackBoxHandle)` as local persistent ownership.
  That was wrong for `WfcOutpostPowerBootRuntime`, where the backing owner is `GlobalDataVault`.
- The scanner did not understand private-ref aliases to nested native buffer sets, e.g. `_saveTelemetryRing => ref _nativeBuffers.SaveTelemetryRing`.
  That made valid owner-local or Vault-backed buffers look unowned or declared-only when the field itself was public inside a private holder type.
- Two signal-like DTOs lacked explicit layout: `FaunaDirector.AcousticPanicCommand` and `VocalWarningSystem.VocalWarningTelemetrySnapshot`.

## What Was Done

- Added a global struct-layout index to `SignalBusContractAuditCli`.
- Added expression-bodied accessor rejection to `TelemetryArrayRegex`.
- Added nested/native field alias handling for private ref properties, member-access dispose helpers, H8Memory release helpers, and DataVault allocator aliases.
- Split `GlobalTelemetryBus._snapshotBuffer` into an owner-local telemetry export staging classification instead of treating it as persistent blackbox authority.
- Added explicit 32-byte layout for `AcousticPanicCommand`.
- Added explicit 48-byte layout for `VocalWarningTelemetrySnapshot`.

## Proof

- Scoped `git diff --check` after the final borrowed-view source patch passed.
  Output only line-ending normalization warnings; no whitespace/error rows.
- Brace balance for touched runtime files:
  - `FaunaDirector.cs`: `0`
  - `VocalWarningSystem.cs`: `0`
- CLI build before the final borrowed-view classifier refinement: `Docs/Reports/BUILD_UNKNOWN_SIGNAL_CLI_LAYOUT_ALIAS_RECHECK10_20260527.log`
  - Result: `0 Warning(s)`, `0 Error(s)`.
- Fresh audit from that build: `Docs/Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_LAYOUT_ALIAS_RECHECK2.json`
  - `errors=0`
  - `confirmedErrors=0`
  - `warnings=245`
  - `infos=1082`
  - `CACHELINE_CRITICAL_SIGNAL_STRIDE_DEBT=1`
  - `SIGNAL_LAYOUT_REVIEW=0`
  - `LOCAL_NATIVE_TELEMETRY_STAGING_BUFFER_OWNER_LOCAL=1`
  - `LOCAL_NATIVE_TELEMETRY_RING_OWNER_LOCAL=7`
  - `LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT=3`

## Current Verification Gap

After the audit above, the classifier was tightened once more to classify public/internal `NativeArray<Telemetry...>` fields inside structs as borrowed views when no same-file allocation exists.
That final source state has not been rebuilt yet because the CPU guard stayed above 50% for repeated checks, including a later `100%` sample.
AGENTS forbids `dotnet build` under that load.

Blocked proof target:

- Rebuild `SignalBusContractAuditCli` in `UnknownCheck`.
- Rerun full audit to replace `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_LAYOUT_ALIAS_RECHECK2.*`.

## Residual Real Debt

- `TetherTensionSignal` remains the only verified cache-line-critical stride debt from the audited build.
  Its payload is intentionally 192 bytes because it carries two `AbsoluteUniversePosition` values plus tension state.
  Correct fix is not padding; it is a future split between compact gameplay tension truth and visual sidecar if profiling proves this lane hot enough.
- `ModEventProjectionBridge._cullTelemetry` remains registered non-Vault blackbox state without a proven dump route.
- `TBDRPipelineSurgeonTypes` still has two registered non-Vault telemetry buffers in graphics/culling ownership, outside this core pass.

Runtime microseconds saved: `0` claimed. This pass removed false work and locked layout contracts; no profiler/player run was performed.
