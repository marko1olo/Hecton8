# HECTON-8 Build / Playtest Issues

Date: 2026-05-26
Status: PENDING VERIFICATION
Owner: build/playtest issue anchor
Evidence: STATIC_DOC only unless a build/playtest artifact is cited

## Authority

This file tracks current player-facing blockers only. Historical full ledger copy:

- `Docs/DEPRECATED/Root_Bloat_X_012_2026-05-23/BUILD_PLAYTEST_ISSUES.md`

Do not mark `[x]` without current player build, Play Mode, user confirmation, profiler, GCMonitor, or visual artifact as appropriate.

## Current Build Evidence

Latest local full-solution CLI PASS:

- `Docs/Reports/BUILD_UNKNOWN_RECHECK_20260526_020504.log`
- Command: `dotnet build .\Hecton8.slnx -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false`
- Exit code: `0`
- Proof lines: `66 Build succeeded.`, `67 0 Warning(s)`, `68 0 Error(s)`
- Evidence class: CLI_COMPILE only

This supersedes older root-doc statements that compile was blocked by CPU/compiler contention. It does not prove Unity import, Play Mode, player build, profiler, GC, scene wiring, or visual quality.

Not proven by that log:

- Unity import
- Unity Console
- Play Mode
- player build
- profiler/GCMonitor
- save/load
- scene wiring
- visual quality
- platform readiness

## Open Product Blockers

| Blocker | Status | Proof Needed |
|---|---|---|
| Surface transition hitch | `[c]` | player/build swim while crossing surface and rotating camera |
| Surface oxygen refill | `[c]` | depleted-O2 surfacing test in build |
| Pause cursor and button focus | `[c]` | build check for cursor, lock state, Esc flow, button actions |
| Surface/interior/underwater audio | `[~]` | snapshot assets, runtime transition proof, player ambient source verification |
| Menu -> world start context | `[c]` | clean new/load/resume path in build |
| Save/load return route | `[~]` | current write/read/migration/corruption artifact |
| First 20 Minutes Copper Wire route | `[~]` | full route clip plus profiler/GC/memory capture |
| Data Monolith runtime boot | `[~]` | Unity import/player boot/checksum proof for `static_data.h8bin` |
| RT/VRAM retained owner set | `[!]` | Memory Profiler / Frame Debugger owner isolation |

## Entry Template

```md
## Build Entry - YYYY-MM-DD - Build Name
- Artifact:
- Hardware:
- Scene:
- Status: [ ] / [~] / [c] / [x] / [!] / [?]
- Evidence class:
- Main blocker:
- Change tested:
- Result:
- Failed:
- Next proof:
```

## Rules

- `[c]` means implementation/static-doc work closed, proof pending.
- `[x]` means current artifact proves the claim.
- Build feel beats editor feel.
- Player route proof beats subsystem count.
- Static source, H-Phi, route cards, and compile logs do not prove runtime quality.
- Visual tasks need screenshot/clip proof.
- Performance tasks need profiler/GC/memory evidence.
- Save tasks need write/read/corruption/migration evidence.
