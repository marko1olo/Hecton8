# V0-L07 — P0 boot fix re-probe runbook

UTC written: 2026-07-30T20:21Z
Parent fail: V0-L06 MEASURED FAIL (Environment / OceanKinematics; menu never eligible)
HEAD at write: see git log at commit time

## Architect stance (honest)

P0 is a **defensive product patch** for telemetry dump hard-fail + swallowed bootstrap exceptions + caustics reflection throws.
It is **NOT** yet a proven fix for `OceanKinematicsRuntimeService` root cause — L06 never captured Ocean exception text.
Captain checklist remains **all open**. Zero PLAYER PNGs still means zero PLAYER progress.

Critique (subagent): dump stack and Ocean init stack were concurrent/disjoint; do not claim Ocean fixed until post-P0 probe shows Environment complete OR prints full Ocean exception.

## P0 product changes (real code, not mocks)

| File | Change |
| --- | --- |
| `Assets/_Project/Scripts/Core/Contracts/CoreLowLevelUtilities.cs` | `CreateTransientPayload` returns untracked payload when `NativeMemoryTrackingBridge` not installed; `DisposeTransientPayload` frees untracked without throw |
| `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs` | `DumpCelestialTelemetry` catches `InvalidOperationException` + `Exception` (diagnostics-only) |
| `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` | Log full `exception` on dependency fail; Ocean caustics call try/catch; caustics `Invoke` try/catch → named degrade |
| `Docs/PLAYTEST/V0_VERTICAL_SLICE_EVIDENCE_2026-07-30.md` | V0-L06 MEASURED FAIL + P0 note; checklist still open |

## Explicit rejects

- `-h8ForceMenuLoad` / `forceMenuLoad=true` — mock menu on dead boot
- `-h8headless` as play proof — different product path
- `EmergencyMockOcean` as V0 play provider
- `-nographics` for PLAYER PNG rows — `H8_PlayModeScreenshotter` refuses pixels
- Captain checklist `[x]` without PLAYER screenshots under `Docs/Screenshots/V0_Playtest/`
- Committing `Tools/_cline_scratch/**` or `*.bak_v0boot`

## Success criteria (ordered)

1. **MEASURED boot gate:** playprobe without forceMenuLoad: Environment phase completes OR Ocean node fails with **full exception.ToString()** in log.
2. **Menu eligible:** live `MainMenuController` within wait window (or WORLD if direct).
3. **PLAYER graphics:** Boot→Menu→New Game→WORLD with PNGs V0-S01..S03 in `Docs/Screenshots/V0_Playtest/` (graphics-on; not nographics).
4. Swim ~30s, one tool, one fauna, death/save — only after WORLD.

## Unity

- Editor: `C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe`
- Project: `C:\hades\Hecton8`
- executeMethod: `Hecton8.Editor.Diagnostics.H8_HeadlessPlayModeProbe.RunFromCommandLine` (same as L06)

## Launch (scratch bat — do not commit)

`Tools/_cline_scratch/launch_v0_L07_bootfix_probe.bat`

Flags:
- `-batchmode` **with graphics** (NO `-nographics`)
- NO forceMenuLoad
- NO `-h8headless` as play proof
- Log: `Docs/AgentLogs/h8_playprobe_v0_L07.log`
- JSON: `Docs/AgentLogs/h8_playprobe_v0_L07.json` (if probe writes via -h8PlayProbeJson or default)

## Implemented but NOT integrated to gameplay (debt)

- FaunaBrain / fauna host placement — code exists; WORLD path never reached
- Life-pod / first exit — CONTENT-BLOCKED (no prefab site)
- Hazard AddComponent sites — CONTENT-BLOCKED
- PlayModeSmokeTester — editor harness; not captain PLAYER proof alone
- Ecosystem population solve / biomass — headless ecology ≠ play
- KCC Escape|SdfInvalid mass failures — separate precision debt

## Git allowlist for this fix commit

- The three CS product files above
- `Docs/PLAYTEST/V0_VERTICAL_SLICE_EVIDENCE_2026-07-30.md`
- `Docs/AgentLogs/V0_L07_P0_BOOTFIX_RUNBOOK.md`
- Prior L06 evidence if not already on remote: `h8_playprobe_v0_L06.json`, `V0_L06_PROBE_RUNBOOK.md`, KCC gate JSON/log (optional; large .log may stay local)

Denylist: `Tools/_cline_scratch/**`, `*.bak_v0boot`, tokens, Library, XR noise.
