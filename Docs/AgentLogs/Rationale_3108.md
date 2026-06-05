# Rationale_3108

Status: STATIC VERIFIED / RUNTIME PROOF PENDING
Date: 2026-06-05

## Mandates followed

- `.agents-skills/UI_Data_Streaming_ZeroGC_Optimization.txt`
- `.agents-skills/UI_Diegetic_Physical_Interfaces.txt`
- `.agents-skills/CORE_Abyss_Survival_Systems_O2_Pressure_Logic.txt`
- `.agents-skills/CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `.agents-skills/PROG_Quest_State_Graph_Logic.txt`
- `.agents-skills/DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/ARCH_Signal_Lane_Segregation.txt`

## Authority read

Read: `AGENTS.md`, `TASTE.md`, `VISION_LOCKS.md`, `quality.md`, `gameplay.md`, `ui.md`, `player.md`, `survival.md`, `tools.md`, `narrative.md`, `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md`, `Docs/ARCHITECTURE/FIRST_20_MINUTES_ROUTE_BRIEF.md`, task file, and required Batch31 blocker reports.

## Decisions

1. Status remains `STATIC VERIFIED / RUNTIME PROOF PENDING`.
Reason: task produced route/UI acceptance criteria only. No Unity run, profiler, GC, screenshot, or save/load proof was created.

2. Copper is not accepted as the active first-route spine.
Reason: static report proves copper requires Drill and the starter Drill route is missing. Treating copper as playable would fake the resource/tool/craft chain.

3. Preferred static route is `Data_FiberKelp -> Comp_FiberMesh -> Comp_PressureSeal -> visible pressure-boundary repair`.
Reason: FiberKelp is shallow/reachable by static data, PressureSeal is already accepted by `FirstHourDirector` as a first craft result, and the result can visibly change route safety.

4. HUD/UI proof must be tied to runtime source owners, not screenshot filenames.
Reason: player/HUD bootstrap binding is blocked by scene shell evidence. UI acceptance requires active production player/HUD graph readback and zero-GC text/update proof.

5. Scenic rest is allowed only when bounded by return path and survival instruments.
Reason: vision locks allow calm looking time, but taste/gameplay reject empty beauty shots. Each view must sharpen a decision or prove route/evidence state.

6. No runtime wiring was invented.
Reason: 3108 owns static route/UI acceptance and implementation order only. Scene/prefab/player/HUD fixes belong to Unity/player/UI owners.

## Regression model

- CPU: no runtime code changed.
- GC: no runtime code changed; UI route requires zero-GC char-buffer/TMP proof when implemented.
- Memory/VRAM: no assets or render targets changed.
- Cadence: implementation order requires UI writes in `VISUAL_SYNC`, survival/tool truth from owner snapshots, and no hot registry polling.
- Correctness: main risk is later agents laundering static route text into runtime readiness. Report labels all runtime claims pending.

## Low / Middle / High / Ultra consequences

- Low: oxygen/depth/pressure, return cue, interaction prompt, and repair state must remain readable with reduced motion and static atlases.
- Middle: add richer pinger/service-buoy feedback, material response, and scanner/PDA short forms.
- High: add denser environmental evidence, better seal feedback, richer visor degradation.
- Ultra: add secondary black-box/archive/sensor detail only after route truth and compact readability hold.

