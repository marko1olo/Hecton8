# SHINOBU_02 Signal Bus Contract Audit CLI

Evidence Class: STATIC_SOURCE_CLASSIFIED
Scope: SignalCritical
Generated UTC: 2026-05-18T13:53:35.2350764Z

## Summary

- Files scanned: 8 C# / 61 compute
- Signal-like definitions found: 180
- Signal definitions still in Core/GlobalSignals.cs: 162
- Pack=1 layouts: 0
- Runtime signal Pack=1 layouts: 0
- Signal-like definitions without nearby StructLayout: 1
- Managed event surface hits: 0
- Local native telemetry ring hits: 1
- Registered local telemetry rings: 0
- Local native signal queue hits: 0
- Compute 1024-thread-group hits: 0
- Hot-path heuristic hits: 0
- Cold/fatal sync I/O review hits: 9
- Assembly contract boundary hits: 0
- Errors: 0
- Warnings: 0
- Infos: 10
- Confirmed/probable errors at confidence >= 90: 0
- Review-only findings below confidence 75: 10

## Rule Breakdown

- COLD_OR_FATAL_SYNC_IO_REVIEW: total 9, errors 0, warnings 0, infos 9, avg confidence 64
- EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW: total 1, errors 0, warnings 0, infos 1, avg confidence 56

## Classification Breakdown

- COLD_OR_FATAL_IO_BOUNDARY: 9
- EDITOR_ONLY_REVIEW: 1

## Findings

- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:2629 | DumpMasterPipelineTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:2630 | DumpMasterPipelineTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (System.IO.FileStream stream = System.IO.File.Open(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:2737 | ParseMasterExecutionPriorityCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3250 | DumpDispatcherBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3267 | DumpDispatcherBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (System.IO.FileStream stream = System.IO.File.Open(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs:18 | _telemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<SignalLaneTelemetry> _telemetry;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:161 | TryLoadFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:333 | DumpToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:335 | DumpToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:422 | TryLoad
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.

## Non-Claims

- This audit does not prove Unity import, player build, IL2CPP, runtime GC, profiler, scene wiring, or actual struct sizeof(T).
- Static confidence is not semantic proof. This CLI intentionally stays outside Unity and uses standard .NET only.
- This audit reports contract debt only. It does not modify runtime contracts.
