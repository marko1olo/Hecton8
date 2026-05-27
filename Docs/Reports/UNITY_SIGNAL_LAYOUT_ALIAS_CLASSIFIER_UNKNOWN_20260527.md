# Unity Signal Layout/Alias Classifier Pass - UNKNOWN - 2026-05-27

Evidence class: static source/tooling, final CLI build/audit verified.

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
- CLI build after the final borrowed-view classifier refinement: `Docs/Reports/BUILD_UNKNOWN_SIGNAL_CLI_LAYOUT_ALIAS_FINAL2_20260527.log`
  - Result: `0 Warning(s)`, `0 Error(s)`.
- Fresh audit from that build: `Docs/Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_LAYOUT_ALIAS_FINAL.json`
  - `errors=0`
  - `confirmedErrors=0`
  - `warnings=155`
  - `infos=1171`
  - `CACHELINE_CRITICAL_SIGNAL_STRIDE_DEBT=1`
  - `SIGNAL_LAYOUT_REVIEW=0`
  - `LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY=0`
  - `LOCAL_NATIVE_TELEMETRY_STRUCT_VIEW_REVIEW=47`
  - `LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW=264`
  - `LOCAL_NATIVE_TELEMETRY_STAGING_BUFFER_OWNER_LOCAL=1`
  - `LOCAL_NATIVE_TELEMETRY_RING_OWNER_LOCAL=7`
  - `LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT=3`

## Closed Verification Gap

The CPU guard later dropped to `42%` with no active compiler processes.
The first `UnknownCheck` build attempt hit `CS2012` access denied on the existing `obj` output.
The verified build used a separate `UnknownFinal` configuration and completed with `0` warnings and `0` errors.

## Residual Real Debt

- `TetherTensionSignal` remains the only verified cache-line-critical stride debt from the final audited build.
  Its payload is intentionally 192 bytes because it carries two `AbsoluteUniversePosition` values plus tension state.
  Correct fix is not padding; it is a future split between compact gameplay tension truth and visual sidecar if profiling proves this lane hot enough.
- `ModEventProjectionBridge._cullTelemetry` remains registered non-Vault blackbox state without a proven dump route.
- `TBDRPipelineSurgeonTypes` still has two registered non-Vault telemetry buffers in graphics/culling ownership, outside this core pass.
- Non-target warning debt in the final audit remains real review work: `SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW=69`, `RUNTIME_SYNC_FILE_IO_REVIEW=65`, `DUPLICATE_SIGNAL_LIKE_NAME_REVIEW=14`, `ZERO_GC_HOT_PATH_ENUMERATION_REVIEW=1`.

Runtime microseconds saved: `0` claimed. This pass removed false work and locked layout contracts; no profiler/player run was performed.
