# SHINOBU_02 Signal Bus Contract Audit

Evidence Class: STATIC_SOURCE_CLASSIFIED
Scope: SignalCritical
Generated UTC: 2026-05-20T11:02:39.7408574Z

## Summary

- Files scanned: 9 C# / 68 compute
- Signal-like definitions found: 180
- Signal definitions still in Core/GlobalSignals.cs: 164
- Pack=1 layouts: 0
- Runtime signal Pack=1 layouts: 0
- Signal-like definitions without nearby StructLayout: 1
- Managed event surface hits: 0
- Local native telemetry ring hits: 2
- Registered local telemetry rings: 0
- Hot-path heuristic hits: 0
- Compute 1024-thread-group hits: 0
- Errors: 0
- Warnings: 1
- Infos: 16
- Confirmed/probable errors at confidence >= 90: 0
- Review-only findings below confidence 75: 2

## Rule Breakdown

- FAULT_BLACKBOX_SYNC_DUMP_REVIEW: total 10, errors 0, warnings 0, infos 10, avg confidence 82
- COLD_BOOTSTRAP_CONFIG_SYNC_IO_REVIEW: total 3, errors 0, warnings 0, infos 3, avg confidence 80
- EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW: total 2, errors 0, warnings 0, infos 2, avg confidence 56
- EDITOR_DEV_CSV_HOTSWAP_SYNC_IO_REVIEW: total 1, errors 0, warnings 0, infos 1, avg confidence 84
- RUNTIME_SYNC_FILE_IO_REVIEW: total 1, errors 0, warnings 1, infos 0, avg confidence 76

## Classification Breakdown

- FAULT_BLACKBOX_SYNC_IO: 10
- COLD_BOOTSTRAP_CONFIG_SYNC_IO: 3
- EDITOR_ONLY_REVIEW: 2
- EDITOR_DEV_CSV_HOTSWAP_SYNC_IO: 1
- IO_PRESSURE_HEURISTIC: 1

## Findings

- [INFO][82%][FAULT_BLACKBOX_SYNC_IO] FAULT_BLACKBOX_SYNC_DUMP_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3292
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: Static context classifies this as dispatcher fault/stall blackbox I/O. Keep call sites one-shot or backoff-gated; do not reuse this path for WAL/save or normal runtime persistence.
- [INFO][82%][FAULT_BLACKBOX_SYNC_IO] FAULT_BLACKBOX_SYNC_DUMP_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3293
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (System.IO.FileStream stream = System.IO.File.Open(`
  Required action: Static context classifies this as dispatcher fault/stall blackbox I/O. Keep call sites one-shot or backoff-gated; do not reuse this path for WAL/save or normal runtime persistence.
- [INFO][82%][FAULT_BLACKBOX_SYNC_IO] FAULT_BLACKBOX_SYNC_DUMP_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3345
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: Static context classifies this as dispatcher fault/stall blackbox I/O. Keep call sites one-shot or backoff-gated; do not reuse this path for WAL/save or normal runtime persistence.
- [INFO][82%][FAULT_BLACKBOX_SYNC_IO] FAULT_BLACKBOX_SYNC_DUMP_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3346
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (System.IO.FileStream stream = System.IO.File.Open(`
  Required action: Static context classifies this as dispatcher fault/stall blackbox I/O. Keep call sites one-shot or backoff-gated; do not reuse this path for WAL/save or normal runtime persistence.
- [INFO][84%][EDITOR_DEV_CSV_HOTSWAP_SYNC_IO] EDITOR_DEV_CSV_HOTSWAP_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3462
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(`
  Required action: Static context classifies this as UNITY_EDITOR/DEVELOPMENT_BUILD tuning I/O. Keep it out of release-player paths or stage it off-thread before production use.
- [INFO][82%][FAULT_BLACKBOX_SYNC_IO] FAULT_BLACKBOX_SYNC_DUMP_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:4433
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: Static context classifies this as dispatcher fault/stall blackbox I/O. Keep call sites one-shot or backoff-gated; do not reuse this path for WAL/save or normal runtime persistence.
- [INFO][82%][FAULT_BLACKBOX_SYNC_IO] FAULT_BLACKBOX_SYNC_DUMP_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:4450
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (System.IO.FileStream stream = System.IO.File.Open(`
  Required action: Static context classifies this as dispatcher fault/stall blackbox I/O. Keep call sites one-shot or backoff-gated; do not reuse this path for WAL/save or normal runtime persistence.
- [INFO][80%][COLD_BOOTSTRAP_CONFIG_SYNC_IO] COLD_BOOTSTRAP_CONFIG_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:162
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Static context classifies this as boot/editor configuration ingestion. Keep it out of Tick/dispatcher hot paths and keep file size bounded by the existing scratch buffers.
- [INFO][80%][COLD_BOOTSTRAP_CONFIG_SYNC_IO] COLD_BOOTSTRAP_CONFIG_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:479
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Static context classifies this as boot/editor configuration ingestion. Keep it out of Tick/dispatcher hot paths and keep file size bounded by the existing scratch buffers.
- [INFO][82%][FAULT_BLACKBOX_SYNC_IO] FAULT_BLACKBOX_SYNC_DUMP_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:817
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Static context classifies this as SignalBus fault blackbox I/O. Keep runtime callers one-shot or 300-frame backoff-gated; do not reuse this path for normal persistence.
- [INFO][82%][FAULT_BLACKBOX_SYNC_IO] FAULT_BLACKBOX_SYNC_DUMP_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:819
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Static context classifies this as SignalBus fault blackbox I/O. Keep runtime callers one-shot or 300-frame backoff-gated; do not reuse this path for normal persistence.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:921
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][82%][FAULT_BLACKBOX_SYNC_IO] FAULT_BLACKBOX_SYNC_DUMP_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:2733
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Static context classifies this as SignalBus fault blackbox I/O. Keep runtime callers one-shot or 300-frame backoff-gated; do not reuse this path for normal persistence.
- [INFO][82%][FAULT_BLACKBOX_SYNC_IO] FAULT_BLACKBOX_SYNC_DUMP_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:2735
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Static context classifies this as SignalBus fault blackbox I/O. Keep runtime callers one-shot or 300-frame backoff-gated; do not reuse this path for normal persistence.
- [INFO][80%][COLD_BOOTSTRAP_CONFIG_SYNC_IO] COLD_BOOTSTRAP_CONFIG_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:2991
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Static context classifies this as boot/editor configuration ingestion. Keep it out of Tick/dispatcher hot paths and keep file size bounded by the existing scratch buffers.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs:19 | _telemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<SignalLaneTelemetry> _telemetry;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs:20 | _frames
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<SignalTelemetryFrame> _frames;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.

## Non-Claims

- This audit does not prove Unity import, player build, IL2CPP, runtime GC, profiler, scene wiring, or actual struct sizeof(T).
- Static confidence is not semantic proof. The next precision step is an out-of-band Roslyn runner using Assets/Plugins/Roslyn without wiring analyzers into Unity projects.
- This audit intentionally reports legacy/shared ownership debt instead of silently modifying cross-domain contracts.

