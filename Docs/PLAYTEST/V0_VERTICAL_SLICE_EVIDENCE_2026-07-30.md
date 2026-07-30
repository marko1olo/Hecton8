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

Analyzed 2026-07-30 (STATIC luminance / dominant-channel pass on disk PNGs). All captures pre-date APPLY `d7e461e67`. **None close any captain checklist row.**

| Path | avgL (approx) | Class | Re-proof? |
|---|---|---|---|
| `Docs/Screenshots/1428_02_world_play_after_player_authoring.png` | ~4.4 | FAIL near-black | YES |
| `Docs/Screenshots/fresh_world_after_descend_1428.png` | ~2.0 | FAIL near-black | YES |
| `Docs/Screenshots/h8_02_world_after_water_cloud_01.png` | ~130.9 | BLUE_WATER_OR_SKY (best legacy) | YES — still pre-APPLY |
| Other `1428_*` / `world_*` WORLD frames | dim teal MIXED | partial visuals only | YES |

**Rule:** Historical PNG without a dated re-capture after `d7e461e67` cannot close a checklist row. New captures must land under `Docs/Screenshots/V0_Playtest/` (dir present, empty as of 2026-07-30).

---

## Log registry

| ID | Path | Kind | Timestamp | Result | Notes |
|---|---|---|---|---|---|
| V0-L01 | `Docs/AgentLogs/H8_V0_PLAYTEST_SMOKE_GATE.json` (+ `v0_kcc_gate_2026-07-30.log`) | KCC headless gate | 2026-07-30 11:32Z | MEASURED FAIL | `overallPass:false` `claimsWorldPlayable:false`. Flags `0x00000042` = Escape\|SdfInvalid. `kccFailureCount=743920`. PrecisionDrift clear. Cone contract pass. **Not WORLD playable.** |
| V0-L02 | `Docs/AgentLogs/worldroot_report_2026-07-30.log` | ReportOnly WORLD root | 2026-07-30 ~15:18 | MEASURED OK | `active:1/inactive:0`; REFUSED re-lift (expected post-APPLY). No APPLY run. |
| V0-L03 | `Docs/AgentLogs/worldroot_apply4.log` | Historical APPLY attempt | 2026-07-29 | HISTORICAL | APPLY landed in git `d7e461e67` |
| V0-L04 | `Docs/AgentLogs/worldroot_report.log` | Historical REPORT | 2026-07-29 | HISTORICAL | Pre-APPLY: active:0, buried under DEPRECATED_STUFF |
| V0-L05 | `Docs/AgentLogs/headless_smoke_20260730_p0fix.log` | Headless ecology batch | 2026-07-30 ~15:19–15:27 | MEASURED finished | Short-circuit MEASURED: `Headless SceneActivate short-circuit: MarkMainMenuReached on bootstrap`. Batch exited via `CompleteAfterPlayStopped`. **No biomass / ecology-day PASS lines** in log. Not PLAYER; does not close checklist. |


---

## WORLD root status (EDITOR + MEASURED ReportOnly)

| Fact | Value | Class |
|---|---|---|
| Commit | `d7e461e67` lift `--- WORLD ---` out of DEPRECATED_STUFF | EDITOR |
| Scene path | `Assets/_Project/Scenes/02_HECTON_WORLD.unity` | STATIC |
| Size / mtime (disk 2026-07-30) | 6,438,976 bytes / 2026-07-30 02:15 | STATIC |
| ReportOnly 2026-07-30 | `scene='02_HECTON_WORLD' roots=30 graveyard=present worldRootsAtSceneRoot=active:1/inactive:0` | MEASURED (V0-L02) |
| ReportOnly disposition | `REFUSED - an ACTIVE root named '--- WORLD ---' already exists at scene root` | MEASURED — expected; do **not** re-APPLY |
| Side warning | MapMagic TerrainData.size.y 250 vs geology Y-span 12000 | MEASURED (not playability) |
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

1. ~~ReportOnly WORLD root (no APPLY) — log to V0-L02.~~ **DONE** MEASURED 2026-07-30 (active:1, REFUSED expected).
2. Human or instrumented Play Mode: boot → WORLD; capture V0-S01..S03 under `Docs/Screenshots/V0_Playtest/`. **OWED — dir empty; checklist still all open.**
3. One tool use + one fauna sighting + death/respawn + save roundtrip; capture V0-S04..S07. **OWED.**
4. ~~Run KCC V0 gate (V0-L01).~~ **DONE** MEASURED FAIL 2026-07-30 11:32Z — flags Escape\|SdfInvalid (`0x42`), failureCount 743920, PrecisionDrift clear, `claimsWorldPlayable:false`. Log + JSON under `Docs/AgentLogs/`.
5. KCC regression debt (not playability): diagnose Escape+SdfInvalid mass failures in Shinobu355 smoke — separate from PLAYER route.
6. Only after PLAYER rows pass: integrate missing systems that block those rows (colliders, fauna placement, FaunaBrain host, save HUD failure path).
7. ~~Git: `pull --no-rebase` then push.~~ **DONE** merge `277b44d6c` → pushed `gitlab/main`. Further allowlist commit for V0-L01 artifacts pending this turn.
8. Headless ecology (V0-L05): finished; short-circuit OK; **no non-zero biomass proof** — ecology PLAY proof still open (not PLAYER).

---

## Change log

| When | What |
|---|---|
| 2026-07-30 | Ledger created. Checklist all open. Historical screenshots catalogued as non-closing. Meta-freeze + README + BUILD_PLAYTEST honesty + V0 KCC gate added in working tree. |
| 2026-07-30 15:18 | ReportOnly ran (V0-L02): active WORLD root confirmed; tool REFUSED re-lift (expected). Historical PNG analysis: 2/10 near-black, all pre-APPLY. Headless bootstrap handoff short-circuit in `GameBootstrapper` (stale PlayerPrefs no longer deadlocks batch ecology). |
| 2026-07-30 15:27 | Headless ecology batch finished (V0-L05): short-circuit MEASURED; CompleteAfterPlayStopped; no biomass PASS. |
| 2026-07-30 11:32Z / 15:33 local | V0 KCC gate ran (V0-L01): MEASURED FAIL flags `0x42` Escape\|SdfInvalid; claimsWorldPlayable false. Captain checklist unchanged (all open). |



## V0-L06 — Boot route playprobe (MEASURED FAIL) — 2026-07-30T20:09Z

| Field | Value |
| --- | --- |
| Evidence class | **MEASURED** (batchmode playprobe; not PLAYER) |
| Artifact | `Docs/AgentLogs/h8_playprobe_v0_L06.json` + `.log` |
| UTC | 2026-07-30T16:30:18Z |
| exitCode | 1 |
| failures | 3 |
| finalPhase | LeavingPlayMode |
| scene stayed | `00_BOOTSTRAP` |
| forceMenuLoad | false (correct — forcing menu is a mock) |
| worldDriver.started | false |
| Boot | FAIL — allSystemsReady=False gameReady=False activationStep='Not started' |
| WorldLoad | BLOCKED — no live MainMenuController in 120s |
| Swim/Tool/Resource/Craft/Mission/Hazard/SaveLoad | NOT_EXERCISED |
| FirstExit / Hazard | CONTENT-BLOCKED (no life-pod prefab; no hazard AddComponent sites) |
| Screenshots | none — `Docs/Screenshots/V0_Playtest/` empty; `-nographics` cannot close PNG rows |
| Captain checklist | **still all open** — MEASURED ≠ PLAYER |

### Root cause (MEASURED from log ~2099–2241)

1. Environment phase node `OceanKinematicsRuntimeService` reported **Bootstrap dependency exception** (exception text was **swallowed** by bootstrap logger — only the label was printed).
2. Concurrent LateFrameTick: `HectonSeismicTideDirector.WriteCelestialTelemetryDump` → `NativeFaultDumpWriter.CreateTransientPayload` threw  
   `InvalidOperationException: NativeMemoryTrackingBridge registration failed for NativeFaultDumpWriter transient payload`  
   (`CoreLowLevelUtilities.cs`). Dump only caught IO/Unauthorized, so the throw escaped and could poison boot.
3. Core services (Dispatcher/TickManager/Save/ObjectPool) OK; Environment phase never completed → menu never eligible → probe waited 120s → FAIL.

### P0 product fix applied (this session — real integration, not mocks)

| Change | Why real |
| --- | --- |
| `CreateTransientPayload` returns untracked payload when bridge not installed; dispose matches | Tracking is diagnostics; dump must not hard-kill boot |
| `DumpCelestialTelemetry` also catches `InvalidOperationException` / `Exception` | Telemetry dump is non-critical side channel |
| Ocean node wraps caustics registration; caustics `Invoke` try/catch | Cosmetic caustics must not fail Ocean startup-graph node |
| Bootstrap logs `exception.ToString()` on dependency exception | Next failure is diagnosable |

### Explicitly rejected as "fixes"

- `-h8ForceMenuLoad` / forceMenuLoad=true — mock menu on dead boot
- `-h8headless` ecology short-circuit as play proof — different product path
- EmergencyMockOcean as V0 play provider
- Marking captain checklist without PLAYER screenshots + controllable spawn

### Next proof gate after recompile

1. Re-run playprobe **without** forceMenuLoad; expect Boot beyond Environment (menu eligible or WORLD).
2. Graphics-on Boot→Menu→New Game→WORLD; capture V0-S01..S03 under `Docs/Screenshots/V0_Playtest/`.
3. Swim ~30s (V0-S03), one tool (V0-S04), one fauna (V0-S05); then death/save.


## V0-L07 — P0 bootfix re-probe (MEASURED progress) — 2026-07-30T20:29Z

| Field | Value |
| --- | --- |
| Evidence class | **MEASURED** (batchmode playprobe, graphics flags ON / no `-nographics`; not PLAYER PNGs) |
| HEAD | `1b1596859` (P0 product + bak quarantine on main; pushed gitlab+origin) |
| Artifact log | `Docs/AgentLogs/h8_playprobe_v0_L07.log` |
| Artifact JSON | `Docs/AgentLogs/h8_playprobe_v0_L07.json` (probe claimed write; verify on disk) |
| executeMethod | `Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe.Run` |
| forceMenuLoad | **false** (correct) |
| -nographics | **not passed** |
| -h8headless | **not passed** as play proof |
| activeScene end | `02_HECTON_WORLD` |
| Captain checklist | **still all open** — zero PLAYER PNGs under `Docs/Screenshots/V0_Playtest/` |

### Route moments (from log)

| Moment | Result | Notes |
| --- | --- | --- |
| Boot | **PASS** | allSystemsReady=True gameReady=True activationStep=Complete activeScene=02_HECTON_WORLD |
| WorldLoad | **PASS** | 02_HECTON_WORLD loaded ~10s; 01_MAIN_MENU unloading |
| FirstExit | NOT_EXERCISED | CONTENT-BLOCKED — no life-pod/drop-pod prefab sites |
| Swim | **FAIL** | 93935 input overrides; movementIntent01max=0.000; depth span=0; immersionMax=1.000 — input plumbing / intent not reaching movement |
| Resource | BLOCKED | node nearly depleted (260→0.444) but not closed |
| Tool | BLOCKED | slotCount=4 but IsToolAvailableInSlot false all slots; inventory version stuck 0 |
| CraftRepairBuild | BLOCKED | fabricator live; 0 recipes craftable (no resource delivery) |
| Mission | BLOCKED | 12 quests authored; 0 completions |
| Hazard | NOT_EXERCISED | CONTENT-BLOCKED — no hazard AddComponent sites |
| SaveLoad | PARTIAL | save half observed (slot_0 file change); load half not exercised |
| Proof | PARTIAL | log + phase table; determinism NeverSampled |

Aggregate: pass=2 partial=2 fail=1 blocked=4 notExercised=2 / 11. RESULT failures=1 (Swim).

### Ocean / Environment (P0 gate)

- L06: Environment died on `OceanKinematicsRuntimeService` (exception text swallowed) + concurrent celestial dump throw.
- L07: `TryInitializeBootstrapDependencyNodeWithFallback for node OceanKinematicsRuntimeService` + `Waiting for heartbeat for node OceanKinematicsRuntimeService` — **no** `Bootstrap dependency exception` / phase fail for Ocean.
- Boot moment PASS with activation Complete ⇒ Environment phase completed. P0 logger + dump hardenings held for this route.
- Critique stance retained historically: L06 dump≠proven Ocean root; L07 now **measures** boot past Environment without needing forceMenuLoad.

### Explicit non-claims

- Not PLAYER. `Docs/Screenshots/V0_Playtest/` still **empty**.
- Swim FAIL means vertical slice is **not** playable for captain row 3.
- Tool/fauna/death rows remain open.
- Batchmode + world-driver input overrides ≠ human control proof.
- Unity ended with mono/native fatal during teardown after moments (log notes); does not erase moment PASS lines already emitted.

### Next product work (ordered)

1. **Swim/input plumbing** — movementIntent stays 0 despite driver overrides (`INPUTHOP` overrideRejected high; publishGuardFail). Fix intent path so Swim can PASS on re-probe.
2. **Tool loadout** — make at least one slot `IsToolAvailableInSlot=true` on New Game spawn (inventory version should move).
3. **Graphics PLAYER PNGs** — Boot→WORLD V0-S01..S03 under `Docs/Screenshots/V0_Playtest/` (human or non-nographics capture that writes pixels).
4. Then fauna ×1, death/respawn, save/load roundtrip.

