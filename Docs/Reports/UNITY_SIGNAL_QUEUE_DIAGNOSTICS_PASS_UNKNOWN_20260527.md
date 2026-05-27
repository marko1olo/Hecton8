# Unity Signal Queue Diagnostics Pass - UNKNOWN - 2026-05-27

Evidence class: static source, toolchain build, and generated contract scan.

## Problem

- `SignalBusContractAuditCli` reported `8` `POSSIBLE_ORPHANED_SIGNAL_QUEUE` warnings after the prior signal-contract pass.
- Manual source proof showed those were not all orphaned lanes:
  `SuitMeshUpdateEvents` and `VehicleCommandSignals` register/unregister/dispose their queues.
- The CLI project targeted `net8.0`, while the available local runtime is `.NET 10.0.6`.
  A normal build produced a `net8.0` binary that could not run on this machine.
- `RuntimeDiagnosticsTrace.FlushSuppressedDuplicates()` allocated a new `List<string>` on flush.

## Changes

- `Tools/SignalBusContractAuditCli/Program.cs`
  - Detects `NativeQueue<T>` allocation in queue ownership checks.
  - Recognizes `RegisterQueue(...)` and `DisposeQueue(...)` helper routes.
  - Classifies non-allocating queue aliases as `LOCAL_SIGNAL_QUEUE_DECLARED_ONLY_REVIEW`.
- `Tools/SignalBusContractAuditCli/SignalBusContractAuditCli.csproj`
  - Retargeted from `net8.0` to `net10.0`, matching the local tool runtime and other project CLI tools.
- `Assets/_Project/Scripts/RuntimeDiagnosticsTrace.cs`
  - Replaced flush-time `new List<string>(Keys)` with an owner-owned reusable list.
  - Replaced `foreach` with explicit dictionary enumerator use to keep the heuristic scan clean.

## Proof

- Tool restore/build: legal guard window at CPU `45`, compiler count `0`; build succeeded with `0` warnings and `0` errors.
- Recheck: `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_QUEUE_RECHECK.json`.
- Recheck result: `errors=0`, `confirmedErrors=0`, `warnings=521`, `infos=692`.
- Queue result: `POSSIBLE_ORPHANED_SIGNAL_QUEUE=0`.
- Queue classifications now include `17` registered local queues and `1` declared-only external queue wrapper.
- `RuntimeDiagnosticsTrace` no longer has the previous flush list allocation or flush `foreach` finding.
- Scoped `git diff --check` passed with LF/CRLF warnings only.
- Documentation gates: `VerifyDocStructure.py pass=true activeDocCount=670 encodingWithoutUtf8Sig=0`.
- Documentation gates: `OOP_Doc_Scanner.py finalPass=true activeFileCount=670 sourceSyncPass=true`.

## Residuals

- `8` `LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT` warnings remain.
  They have sentinel ownership and dump coverage, but still need per-owner Vault migration decisions.
- Full `Hecton8.slnx` build was not launched in this pass:
  `BUILD_UNKNOWN_SIGNAL_QUEUE_DIAGNOSTICS_RECHECK_20260527.log` records `30` blocked guard attempts with CPU above `50%`.
- No profiler, Unity import, Play Mode, Memory Profiler, player build, or visual proof was produced.
- Runtime microseconds saved claimed: `0`.
