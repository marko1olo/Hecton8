# HECTON-8 Build Queue

Top = old, bottom = new. Agents must register before `dotnet build` or Unity refresh.

## 2026-05-11T23:02:03+04:00 | PLATFORM_COMMAND

- Agent: `PLATFORM_COMMAND`
- Host: i5-1135G7, 4C/8T constraint active.
- Build status: registered, no build started by this agent.
- Required build command: `dotnet build <target> --no-restore -m:2 /nr:false`
- Required cleanup: `dotnet build-server shutdown`
- Current gate decision: clear for analysis and file edits only; final compile still pending.
