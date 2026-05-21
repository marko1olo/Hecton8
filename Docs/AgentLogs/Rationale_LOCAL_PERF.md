# Rationale_LOCAL_PERF

Date: 2026-05-22
Status: PENDING VERIFICATION

Problem: VS Code and Codex caused lag during many parallel agents. Local data showed no RAM exhaustion, but sustained CPU and working-set pressure from `codex.exe`, VS Code renderer, extension-host, and transient C#/Roslyn builds.

Solution:
- Used `code --status`, process command lines, working set, CPU samples, VS Code logs, and extension package settings as evidence.
- Applied workspace-level C# background analysis reduction rather than disabling C# tooling globally.
- Disabled VS Code Git UI and broad watcher/search paths for generated Unity, logs, reports, build, and agent folders.
- Lowered process priority for Codex and build tools instead of terminating active work.
- Kept a 600s no-kill watchdog for observation, priority throttling, and checkpoint commits.

Rejected Alternatives:
- Kill `codex.exe`: rejected because it is the active agent transport.
- Kill `dotnet`, `csc`, or `VBCSCompiler` blindly: rejected because active builds belong to other agents.
- Delete VS Code cache/history while VS Code is open: rejected due history/session corruption risk.
- Disable `openai.chatgpt`: rejected because it would stop the current working channel.
- Enable WSL Codex mode: rejected because WSL is not installed.

Scalability potential:
- Low tier i5/MX350: lower background analysis and lower priority build/Codex processes should preserve foreground responsiveness under load.
- Middle tier: same settings reduce unnecessary indexing and decoration work while keeping manual builds intact.
- High tier: settings can be relaxed if interactive C# diagnostics become more valuable than CPU headroom.
- Ultra tier: Codex/C# background features may be re-enabled per workspace after profiler proof.

Hardware Impact:
- CPU: expected improvement is fewer spikes from workspace analysis, Git decoration, and Codex competing at normal priority.
- RAM: no hard shortage observed; memory wins come mainly after VS Code reload and closed-state cache cleanup.
- Estimated low-end gain: not expressed as microseconds without profiler trace; observed symptom gain is renderer dropping from high CPU samples to low single-digit CPU after csc completion and priority throttle.

Verification required:
- Reload VS Code when active agent sessions can survive it.
- Re-run `code --status` after reload.
- Compare 10-minute watchdog samples before/after reload.
- Confirm no loss of C# navigation required by current workflow.
