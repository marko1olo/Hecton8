# SignalBusContractAuditCli

Cold out-of-band C# runner for the signal-contract static audit.

Use this CLI as the default repeatable gate. `Tools/SignalBusContractAudit.ps1` remains the portable fallback, but it is slower on the full tree.

No NuGet packages. The tool reads Unity `.asmdef` metadata for contract-boundary review, but it does not compile Unity assemblies and does not prove runtime, profiler, Play Mode, IL2CPP, or GC behavior.

## Run

```powershell
dotnet run --project Tools/SignalBusContractAuditCli -- --project-root . --json Temp/SignalBusContractAudit.json --markdown Temp/SignalBusContractAudit.md
```

Optional:

```powershell
dotnet run --project Tools/SignalBusContractAuditCli -- --project-root . --json Temp/audit.json --markdown Temp/audit.md --scope SignalCritical --fail-on-error
```

Hot-path heuristics are opt-in because they are intentionally conservative review signals, not confirmed defects:

```powershell
dotnet run --project Tools/SignalBusContractAuditCli -- --project-root . --include-hot-path-heuristics --json Temp/audit_hotpath.json --markdown Temp/audit_hotpath.md
```

Scopes:

- `Full`: scan all first-party scripts plus compute shaders.
- `SignalCritical`: scan core signal-contract surfaces plus compute shaders.

Interpretation:

- `ERROR`: high-confidence static contract breach. Current hard classes include runtime signal `Pack=1`, duplicate runtime signal names, managed string payloads, and unowned native telemetry rings.
- `WARN`: review-required static risk. Current warning classes include synchronous file I/O review, runtime native `Pack=1` candidates outside direct signal definitions, registered non-vault telemetry rings, possible orphaned queues, and missing direct `Hecton8.Core.Contracts` references for signal contract usage.
- `INFO`: intentionally lower-confidence or editor/file-format boundary review.

`COLD_OR_FATAL_SYNC_IO_REVIEW` is downgraded to `INFO` when the enclosing method/file name clearly indicates load, dump, export, validation, or fatal-reporting work. It is still a review item if it can run on the gameplay frame.

Confidence is static source confidence, not runtime proof. A finding can be correct and still require an owning-domain migration plan before code is changed. Hot-path findings are heuristic review prompts; they do not replace Unity Profiler/GCMonitor proof.
