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

## 2026-05-22 11:45 Samara Update

Done:
- VS Code was observed after a reload/new process set: old `codex.exe` PID 34124 was gone, new `codex.exe` PID 45216 started with much lower memory.
- Safe watchdog updated to throttle VS Code utility NodeService processes, not renderer/window/GPU.
- Current `codex.exe` and VS Code utility processes were lowered to `BelowNormal`.
- Added `C:\Users\danat\AppData\Roaming\Code\argv.json` with `"disable-hardware-acceleration": true`; requires full VS Code restart.
- User settings updated to hard-disable bundled GitHub Copilot/Copilot Chat background/cloud/Claude agents, workspace code search, local index, GitHub MCP, Copilot code actions, and Git UI.
- VS Code CLI used to mark bundled `GitHub.copilot-chat`, `vscode.git`, and `vscode.github` disabled for next startup.

Evidence:
- `code --status` after reload: VS Code/Codex working set down to roughly 2.6 GB from previous 4.1-4.4 GB samples.
- `openai.chatgpt` log was noisy: repeated `thread-stream-state-changed` warnings.
- Bundled `GitHub.copilot-chat` activated on startup and logged `TypeError: e is not iterable`; it also activated Git services.
- Git log showed repeated `git status`, `for-each-ref`, `merge-base`, and `diff` activity even after workspace git settings.
- Display path: Intel Iris Xe driver `30.0.101.1191` from 2021 is active for VS Code/DWM; NVIDIA MX350 driver is newer.
- Lightweight 8s CPU delta after WMI diagnostics stopped: `site_tgach` approx 1.7%, VS Code renderer approx 0.5%, Task Manager approx 0.7%. No sustained CPU fire remained.

Deferred:
- Full benefit requires closing/reopening VS Code so bundled Copilot/Git disable and hardware acceleration settings take effect.
