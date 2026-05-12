# HECTON-8 Build Queue

Top = old, bottom = new. Agents must register before `dotnet build` or Unity refresh.

## 2026-05-11T23:02:03+04:00 | PLATFORM_COMMAND

- Agent: `PLATFORM_COMMAND`
- Host: i5-1135G7, 4C/8T constraint active.
- Build status: registered, no build started by this agent.
- Required build command: `dotnet build <target> --no-restore -m:2 /nr:false`
- Required cleanup: `dotnet build-server shutdown`
- Current gate decision: clear for analysis and file edits only; final compile still pending.

## 2026-05-11T23:20:00+04:00 | PLATFORM_COMMAND

- Agent: `PLATFORM_COMMAND`
- Build status: starting final compile.
- Target: `Hecton8.Editor.csproj`
- Command: `dotnet build Hecton8.Editor.csproj --no-restore -m:2 /nr:false`
- Gate decision: no other active build recorded in this queue.

## 2026-05-11T23:20:03+04:00 | PLATFORM_COMMAND

- Agent: `PLATFORM_COMMAND`
- Build status: failed before C# compilation.
- Result: `NETSDK1004`.
- Cause: `Temp/obj/Hecton8.Editor/project.assets.json` is missing; `--no-restore` cannot compile until a restore/Unity project regeneration has produced assets.
- Cleanup: `dotnet build-server shutdown` completed.
- Next gate decision: final compile is blocked by missing restore artifacts, not by reported C# diagnostics.
