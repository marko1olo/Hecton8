# LOG_LOCAL_PERF

2026-05-22T03:55+04:00

What was wrong:
- VS Code 1.121.0 had high renderer/window and extension-host load.
- `openai.chatgpt` Codex app-server was the persistent top CPU consumer.
- C#/Roslyn builds and `VBCSCompiler` created transient high CPU and high working-set pressure.
- Free RAM remained high; lag was primarily CPU/contention/rendering, not physical memory exhaustion.

What was done:
- User settings already reduced updates, experiments, local history, editor decorations, terminal persistence, and extension update checks.
- Workspace settings already disabled Git UI and excluded Unity/generated/agent/report folders from watcher/search.
- Added C# workspace throttles in `.vscode/settings.json` and committed as `2451c0670`.
- Lowered active `dotnet`, `csc`, `VBCSCompiler`, and `codex.exe` priorities to `BelowNormal`.
- Restarted no-kill watchdog as PID 35476; it now also throttles `codex.exe`.
- Watchdog committed active checkpoint as `30ab5e3e6`.

Cinematic cheats used:
- Replaced hot background analysis with explicit/manual build verification where possible.
- Replaced hard process termination with priority throttling.
- Deferred expensive cleanup until VS Code is closed.

Exact microseconds saved:
- Not claimed. No profiler trace exists. Current proof is process-level CPU/working-set sampling only.

External references used:
- VS Code Performance Issues wiki: `https://github.com/microsoft/vscode/wiki/performance-issues`
- VS Code Extension Bisect article: `https://code.visualstudio.com/blogs/2021/02/16/extension-bisect`
- C# Dev Kit FAQ background analysis section: `https://code.visualstudio.com/docs/csharp/cs-dev-kit-faq`
- OpenAI Codex issue #17856: `https://github.com/openai/codex/issues/17856`
- OpenAI Codex issue #18515: `https://github.com/openai/codex/issues/18515`

Current blocked/deferred actions:
- Full VS Code reload is deferred to avoid breaking active agents.
- Cache/history cleanup is deferred until VS Code is closed.
- WSL Codex mode is unavailable because WSL is not installed.

2026-05-22T11:45+04:00

What was wrong:
- After VS Code reload, the old 900MB Codex process was gone, but bundled GitHub Copilot Chat activated on startup.
- Copilot Chat log showed model/session startup and `TypeError: e is not iterable`.
- Git extension remained active and repeatedly ran status/ref/diff commands.
- `dwm.exe`/VS Code renderer had transient high samples. Active display path uses an old Intel Iris Xe driver `30.0.101.1191` from 2021.
- WMI diagnostics themselves caused temporary `Winmgmt/WmiPrvSE` load.

What was done:
- Added `C:\Users\danat\AppData\Roaming\Code\argv.json` with hardware acceleration disabled for next VS Code start.
- Updated user settings to disable bundled GitHub Copilot Chat/background/cloud/Claude agents, workspace code search/local index, GitHub MCP, Copilot code actions, and Git UI.
- Ran VS Code CLI disable for `GitHub.copilot-chat`, `vscode.git`, and `vscode.github`.
- Updated watchdog to throttle VS Code NodeService utility processes while leaving renderer/window/GPU processes alone.

Cinematic cheats used:
- Removed expensive editor AI/Git startup paths instead of trying to out-optimize them.
- Switched from WMI-heavy polling to lightweight CPU deltas for confirmation.

Exact microseconds saved:
- Not claimed. Lightweight 8s delta after stabilization showed no sustained CPU fire: `site_tgach` approx 1.7%, VS Code renderer approx 0.5%.

Still required:
- Full close/reopen of VS Code to apply hardware acceleration and bundled extension disablement.
