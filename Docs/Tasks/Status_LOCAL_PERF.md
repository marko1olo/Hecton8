# Status_LOCAL_PERF

Date: 2026-05-22
Status: PENDING VERIFICATION

Scope: local workstation and VS Code/Codex performance mitigation. No gameplay domain ownership claimed.

Relevant mandates read:
- `AGENTS.md`: evidence-first, no fake readiness, no uncontrolled process disruption.
- `Docs/Actual Domains of Project.txt`: repo domain map; this task is tooling/perf support, not gameplay code.
- `.agents-skills` registry glanced for relevant operational constraints; no game-system mandate applies directly.

Done:
- Disabled VS Code and extension auto-update paths in user settings.
- Reduced VS Code UI/editor churn in user settings: minimap, sticky scroll, inline suggestions, local history, heavy editor decorations, terminal persistence, terminal scrollback.
- Reduced workspace churn in `.vscode/settings.json`: Git UI disabled, watcher/search excludes for Unity and agent/report/log/build folders.
- Added C# workspace throttles:
  - `dotnet.projects.enableAutomaticRestore: false`
  - analyzer diagnostics scope `none`
  - compiler diagnostics scope `openFiles`
  - C# CodeLens and expensive completion paths disabled.
- Lowered priority for active `dotnet`, `csc`, `VBCSCompiler`, and `codex.exe` to `BelowNormal` without killing them.
- Restarted safe watchdog as PID 35476 with 600s interval, no-kill mode, Codex priority throttling enabled.
- Protected and did not stop: `site_tgach`, `dvachbot`, `stomchat`.

Evidence:
- Local VS Code: 1.121.0, commit `f6cfa2ea2403534de03f069bdf160d06451ed282`, installed build timestamp 2026-05-19.
- Earlier watchdog sample: VS Code/Codex working set total 4262.8 MB; top `Code:44388:1194.6MB`, `codex:34124:967.6MB`, `Code:29280:592MB`.
- Post-throttle sample: `codex.exe` approx 46% CPU, VS Code renderer approx 5% CPU, extension-host approx 0% CPU; free RAM approx 16.65 GB of 31.65 GB.
- WSL not installed; `chatgpt.runCodexInWindowsSubsystemForLinux` not enabled.

Deferred:
- VS Code Reload Window/restart is required for full effect but is deferred to avoid interrupting active agent sessions.
- Hot deletion of VS Code `Cache`, `CachedData`, `User/History`, and old `workspaceStorage/chatSessions` is deferred until VS Code is closed.
- Disabling or uninstalling `openai.chatgpt` is not done because this is the active agent channel.

Latest commits:
- `2451c0670 chore: quiet vscode csharp background work`
- `30ab5e3e6 chore: safe watchdog checkpoint 2026-05-22 03:53`
