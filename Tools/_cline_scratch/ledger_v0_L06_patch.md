# Ledger patch text for V0-L06 (main agent INSERT only)

Source of truth: `Docs/AgentLogs/h8_playprobe_v0_L06.json` (schema `hecton8.playprobe.route.v1`)
Companion log: `Docs/AgentLogs/h8_playprobe_v0_L06.log`
Runbook: `Docs/AgentLogs/V0_L06_PROBE_RUNBOOK.md`
Launcher (scratch, not ledger): `Tools/_cline_scratch/launch_v0_L06_probe.bat`

**Verdict policy:** MEASURED FAIL. Do **not** mark any captain checklist row `[x]`. Do **not** invent PASS. Screenshots still empty; `-nographics` cannot close PNG rows.

---

## A) Log registry - INSERT new row after V0-L05

Paste this table row into the **Log registry** section (after the V0-L05 row, before the blank line / next `---`):

```
| V0-L06 | `Docs/AgentLogs/h8_playprobe_v0_L06.json` (+ `h8_playprobe_v0_L06.log`; runbook `V0_L06_PROBE_RUNBOOK.md`) | Headless PlayMode route probe (`H8_HeadlessPlayModeProbe.Run`, `-h8StartGame 1`) | 2026-07-30 16:30:18Z | MEASURED FAIL | Menu gate. `exitCode:1` `failures:3` `finalPhase:"LeavingPlayMode"` `batchmode:true`. Active scene stayed `00_BOOTSTRAP` (`scene` / Boot detail). `moments.Boot=FAIL` (`allSystemsReady=False` `gameReady=False` `activationStep="Not started"` `activationCompleted=False`; Dispatcher/TickManager/Save/ObjectPool present). `moments.WorldLoad=BLOCKED` - `no live MainMenuController in 120s of play, so New Game was never pressed` (`args.menuWaitSeconds:120`). `worldDriver.started:false` `ticks:0`. Top-level `gameFrames:0`; phase `LoadingMenu` wall 120.001s / 7141 gameFrames (~59.5 fps) then Reporting->LeavingPlayMode. Determinism `state=OwnerPresentBufferUnopened` `runComparable:false` hash all zero. Save leg never requested (`save.requested:false`). Downstream moments Swim/Tool/Resource/... = `NOT_EXERCISED`; FirstExit+Hazard = CONTENT-BLOCKED (authoring). Proof=`PARTIAL` (JSON+clocks written; no screenshot/profiler producer). **Not WORLD playable. Does not close captain checklist.** |
```

Optional one-line note under the Log registry table (not a checklist close):

```
V0-L06 screenshots: `Docs/Screenshots/V0_Playtest/` still **empty** after this run. Probe launched with `-batchmode -nographics` - cannot produce or close V0-S0x PNG rows. Graphics capture requires a non-nographics (human or instrumented Game View) pass after boot->menu is fixed.
```

---

## B) Change log - INSERT row

Paste into the **Change log** table (newest at bottom is fine; match existing style):

```
| 2026-07-30 ~16:30Z | V0-L06 headless PlayMode route probe MEASURED FAIL (menu gate). Artifact `Docs/AgentLogs/h8_playprobe_v0_L06.json` utc=`2026-07-30T16:30:18.9765080Z`. `exitCode:1` `failures:3`. Boot FAIL on `00_BOOTSTRAP` (`allSystemsReady=False`, activation never started). WorldLoad BLOCKED: no live `MainMenuController` within `menuWaitSeconds=120`. New Game never pressed; world driver never started; no save leg; no comparable lockstep hash. Captain checklist **unchanged** (all open). `Docs/Screenshots/V0_Playtest/` still empty; `-nographics` cannot close PNG. |
```

---
## C) Next real-game actions - REPLACE the ordered list

Replace the entire **Next real-game actions (ordered)** section body with:

```markdown
## Next real-game actions (ordered)

1. ~~ReportOnly WORLD root (no APPLY) - log to V0-L02.~~ **DONE** MEASURED 2026-07-30 (active:1, REFUSED expected).
2. ~~Run KCC V0 gate (V0-L01).~~ **DONE** MEASURED FAIL 2026-07-30 11:32Z - flags Escape|SdfInvalid (`0x42`), failureCount 743920, PrecisionDrift clear, `claimsWorldPlayable:false`.
3. ~~Headless ecology (V0-L05).~~ **DONE** finished; short-circuit OK; **no non-zero biomass proof**.
4. ~~V0-L06 headless PlayMode route probe (`H8_HeadlessPlayModeProbe`, `-h8StartGame 1`, menu 120 / settle 180 / gameplay 90 / timeout 900).~~ **DONE** MEASURED FAIL 2026-07-30 16:30:18Z - menu gate: stuck `00_BOOTSTRAP`, `allSystemsReady=False`, no live `MainMenuController` in 120s, New Game never pressed (`h8_playprobe_v0_L06.json`). Captain checklist still all open.
5. **NEXT - fix boot -> main menu** so Play Mode leaves `00_BOOTSTRAP` with `allSystemsReady`/`gameReady` and a live `MainMenuController` (or equivalent shipping menu authority) without additive force-load deadlock. Re-probe L06-class route after fix. **Do not** treat `-h8ForceMenuLoad` as success path unless explicitly measured safe.
6. After menu is live: instrumented or human Play Mode boot -> New Game -> WORLD; capture V0-S01..S03 under `Docs/Screenshots/V0_Playtest/`. **OWED - dir empty; `-nographics` cannot close PNG.**
7. One tool use + one fauna sighting + death/respawn + save roundtrip; capture V0-S04..S07. **OWED.**
8. KCC regression debt (not playability): diagnose Escape+SdfInvalid mass failures in Shinobu355 smoke - separate from PLAYER route.
9. Only after PLAYER rows pass: integrate missing systems that block those rows (colliders, fauna placement, FaunaBrain host, save HUD failure path, life-pod/hazard content blocks called out in L06 moments).
10. Git allowlist: force-add only measured V0 docs/artifacts per `commit_v0_allowlist.bat` policy; never stage `Tools/_cline_scratch`, tokens, `.env*`.
```

---

## D) Explicit non-actions (do not do these in the ledger edit)

- Do **not** check any captain checklist box (`[ ]` stays `[ ]` for rows 1-7).
- Do **not** fill Screenshot registry paths/results from this run (all V0-S01..S07 remain PENDING; dir empty).
- Do **not** claim WORLD playable, menu reachable, or save/load exercised.
- Do **not** promote L06 `Proof=PARTIAL` into checklist credit - PARTIAL != pass (probe text: only pass is acceptance).

