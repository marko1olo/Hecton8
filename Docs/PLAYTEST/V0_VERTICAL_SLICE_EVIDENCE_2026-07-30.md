# V0 Vertical Slice — Playtest Evidence Ledger

Date: 2026-07-30  
Project: Hecton8 (`C:\hades\Hecton8`)  
Unity: 6000.5.0f1  
Primary scene (player route): `Assets/_Project/Scenes/02_HECTON_WORLD.unity`  
Policy: **Feature without gameplay is DECLINED.** Static code, batch KCC, SEO, and README are not playability.

---

## Evidence classes (AGENTS.md)

| Class | Means | Counts as playable? |
|---|---|---|
| **PLAYER** | Human or instrumented Play Mode on the real boot→world route with controls | YES (required) |
| **MEASURED** | Profiler / log / JSON from a real run with cited artifact path | partial |
| **EDITOR** | Editor script wrote scene/asset; batchmode without player route | NO |
| **STATIC** | Source / GUID / doc inspection | NO |

No checklist row may be `[x]` without PLAYER (or MEASURED where explicitly allowed below).

---

## Law

1. **Feature without gameplay is DECLINED.**
2. Do not mark `[x]` from static review, KCC headless, or cement commits.
3. Screenshots live under `Docs/Screenshots/V0_Playtest/` for new captures; legacy `Docs/Screenshots/1428_*` are historical and must be re-evaluated against post-APPLY WORLD (`d7e461e67`).
4. Logs live under `Docs/AgentLogs/`.
5. V0 KCC gate PASS ≠ WORLD playable.

---

## Captain checklist (all open until PLAYER proof)

| # | Step | Pass condition | Status | Evidence |
|---|---|---|---|---|
| 1 | Boot → world | Load `02_HECTON_WORLD` (or menu New Game) without critical console errors | `[ ]` | |
| 2 | Spawn | Player at usable AUP; camera/controls respond | `[ ]` | |
| 3 | Swim | Move + look underwater ~30 s; no PrecisionDrift / soft-lock | `[ ]` | |
| 4 | Tools ×1 | Equip and use one tool once | `[ ]` | |
| 5 | Fauna ×1 | See at least one live creature react or swim | `[ ]` | |
| 6 | Death / respawn | Die once; respawn recovers control | `[ ]` | |
| 7 | Save / load | Mid-run save then load restores position + inventory | `[ ]` | |

---

## Screenshot registry

New captures → `Docs/Screenshots/V0_Playtest/`.  
Analyze each: what is visible, what is missing, pass/fail vs checklist row.

| ID | Path | Feature / step | Timestamp (UTC) | Result | Notes |
|---|---|---|---|---|---|
| V0-S01 | | Boot → world | | PENDING | |
| V0-S02 | | Spawn / camera | | PENDING | |
| V0-S03 | | Swim | | PENDING | |
| V0-S04 | | Tools ×1 | | PENDING | |
| V0-S05 | | Fauna ×1 | | PENDING | |
| V0-S06 | | Death / respawn | | PENDING | |
| V0-S07 | | Save / load HUD | | PENDING | |

### Historical screenshots (pre-ledger; NOT auto-pass)

These exist under `Docs/Screenshots/` from prior agent work (prefix `1428_`, `world_*`, menu/orbit). They prove past visual experiments, **not** current post-APPLY V0 checklist completion.

| Path (examples) | What it showed historically | Re-proof needed? |
|---|---|---|
| `Docs/Screenshots/1428_02_HECTON_WORLD_gameview.png` | WORLD game view | YES — after APPLY |
| `Docs/Screenshots/1428_02_world_play_after_player_authoring.png` | Play after player authoring | YES |
| `Docs/Screenshots/1428_menu_to_world_route_result.png` | Menu → world route | YES |
| `Docs/Screenshots/1428_new_dive_route_world_clean_final.png` | New dive route | YES |
| `Docs/Screenshots/world_after_menu_descend_runtime_1428.png` | Descend runtime | YES |
| `Docs/Screenshots/fresh_world_after_descend_1428.png` | Fresh world after descend | YES |

**Rule:** Historical PNG without a dated re-capture after `d7e461e67` cannot close a checklist row.

---

## Log registry

| ID | Path | Kind | Timestamp | Result | Notes |
|---|---|---|---|---|---|
| V0-L01 | `Docs/AgentLogs/H8_V0_PLAYTEST_SMOKE_GATE.json` | KCC headless gate | PENDING | PENDING | Does not claim WORLD |
| V0-L02 | `Docs/AgentLogs/worldroot_report_2026-07-30.log` | ReportOnly WORLD root | PENDING | PENDING | No APPLY |
| V0-L03 | `Docs/AgentLogs/worldroot_apply4.log` | Historical APPLY attempt | 2026-07-29 | HISTORICAL | APPLY landed in git `d7e461e67` |
| V0-L04 | `Docs/AgentLogs/worldroot_report.log` | Historical REPORT | 2026-07-29 | HISTORICAL | Pre-APPLY |

---

## WORLD root status (EDITOR only)

| Fact | Value | Class |
|---|---|---|
| Commit | `d7e461e67` lift `--- WORLD ---` out of DEPRECATED_STUFF | EDITOR |
| Scene path | `Assets/_Project/Scenes/02_HECTON_WORLD.unity` | STATIC |
| Size / mtime (disk 2026-07-30) | 6,438,976 bytes / 2026-07-30 02:15 | STATIC |
| Play Mode boot | **unproven** | — |
| Do not re-run APPLY | yes — use ReportOnly only | process |

Optional verify:
```
"C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\hades\Hecton8" -executeMethod Hecton8.EditorTools.Authoring.H8_WorldRootGraveyardRepair.ReportOnly -logFile "C:\hades\Hecton8\Docs\AgentLogs\worldroot_report_2026-07-30.log"
```

---

## KCC merge gate (not playability)

```
"C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\hades\Hecton8" -executeMethod Hecton8.Physics.KCC.Editor.H8_V0PlaytestSmokeGate.RunFromCommandLine -logFile "C:\hades\Hecton8\Docs\AgentLogs\v0_kcc_gate_2026-07-30.log"
```

- Menu: `Hecton8/QA/V0 Playtest Smoke Gate (KCC headless)`
- Result JSON: `Docs/AgentLogs/H8_V0_PLAYTEST_SMOKE_GATE.json`
- `claimsWorldPlayable: false` always

---

## Integration debt (implemented, not proven in gameplay)

| System | Where it lives | Why not gameplay |
|---|---|---|
| HectonVoxelVolume + collider chunk bake | World scripts | Not on critical proven play path; fall-through unproven |
| FloraGenomeVaultRuntime | Flora | Lab / zero production callers |
| Fauna VAT swarm | Fauna assets | Prefab degeneracy; binder refuses; not player-visible swarm |
| Forge FBX set | Art/Forge | On disk, not placed as live content proof |
| FaunaBrain | AI | GUID only in own `.meta` — no creature carries it |
| WorldChunkResidencyManager | World | Deliberate non-construction (installer notes) |
| WorldContentSocket ×14 | Scene | Shipping filter drops most; post-APPLY live count unproven in Play Mode |
| Headless ecology non-zero biomass | QA Headless | `-h8headless` skips Player; zeros were failure defaults |
| SwimPresentationProfile lower body | Player swim rig | Parameter accepted, never read for legs/fins |
| EditMode 2k+ tests | Tests | `NEVER_COMPILE_TESTS` re-enabled — suite dark |
| Isolated smoke MBs (save/fauna/tools) | various | No single WORLD player-route gate |
| PlayModeSmokeTester | Editor | Menu + sandbox only — **never loads WORLD** |

Bootstrap does wire many runtime installers (`GameBootstrapper` → SaveManager, EcosystemDirector, FaunaDirector resolve, ToolsRuntimeInstaller, PDA/Progression/Narrative/Audio on player, etc.). **Wiring in source ≠ PLAYER proof.**

---

## Boot path (build settings)

Enabled scenes (shipping route intent):  
`00_BOOTSTRAP` → `01_MAIN_MENU` → `01_ORBIT` → `02_HECTON_WORLD`.

Player production authority: `HectonPlayerMovement` as `IBootstrapProductionPlayerMovementAuthority` on `Player.prefab`. Spawn via `HectonPlayerSpawner` after world activation.

---

## Subagent consensus (2026-07-30)

- **DISCOVER:** No PLAYER-class proof WORLD is playable post-APPLY. Route capture dirs empty for current V0. Spawn depends on terrain/vegetation readiness paths that remain risk.
- **CRITIQUE:** V0 gate is honest (no WORLD claim). Dirty tree allowlist = docs + gate + meta-freeze + README badges. Deny `Tools/_cline_*`, XR OpenXR noise, tokens. Diverged main (ahead/behind) needs merge pull, never force.
- **WRITE:** This ledger. Screenshots must be regenerated under `V0_Playtest/` and analyzed before any `[x]`.

---

## Next real-game actions (ordered)

1. ReportOnly WORLD root (no APPLY) — log to V0-L02.
2. Human or instrumented Play Mode: boot → WORLD; capture V0-S01..S03.
3. One tool use + one fauna sighting + death/respawn + save roundtrip; capture V0-S04..S07.
4. Run KCC V0 gate for regression lock only (V0-L01).
5. Only after PLAYER rows pass: integrate missing systems that block those rows (colliders, fauna placement, FaunaBrain host, save HUD failure path).

---

## Change log

| When | What |
|---|---|
| 2026-07-30 | Ledger created. Checklist all open. Historical screenshots catalogued as non-closing. Meta-freeze + README + BUILD_PLAYTEST honesty + V0 KCC gate added in working tree. |
