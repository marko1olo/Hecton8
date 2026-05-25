# SHINOBU_02 Signal Bus Contract Audit

Evidence Class: STATIC_SOURCE_CLASSIFIED
Scope: SignalCritical
Generated UTC: 2026-05-20T02:38:29.8271871Z

## Summary

- Files scanned: 8 C# / 68 compute
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
- Warnings: 15
- Infos: 2
- Confirmed/probable errors at confidence >= 90: 0
- Review-only findings below confidence 75: 2

## Rule Breakdown

- RUNTIME_SYNC_FILE_IO_REVIEW: total 15, errors 0, warnings 15, infos 0, avg confidence 76
- EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW: total 2, errors 0, warnings 0, infos 2, avg confidence 56

## Classification Breakdown

- IO_PRESSURE_HEURISTIC: 15
- EDITOR_ONLY_REVIEW: 2

## Findings

- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3243 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3244 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (System.IO.FileStream stream = System.IO.File.Open(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3296 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3297 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (System.IO.FileStream stream = System.IO.File.Open(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3413 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:4277 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:4294 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (System.IO.FileStream stream = System.IO.File.Open(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:162 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:479 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:809 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:811 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:911 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:2723 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:2725 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:2981 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs:20 | _telemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<SignalLaneTelemetry> _telemetry;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs:21 | _frames
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<SignalTelemetryFrame> _frames;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.

## Non-Claims

- This audit does not prove Unity import, player build, IL2CPP, runtime GC, profiler, scene wiring, or actual struct sizeof(T).
- Static confidence is not semantic proof. The next precision step is an out-of-band Roslyn runner using Assets/Plugins/Roslyn without wiring analyzers into Unity projects.
- This audit intentionally reports legacy/shared ownership debt instead of silently modifying cross-domain contracts.

