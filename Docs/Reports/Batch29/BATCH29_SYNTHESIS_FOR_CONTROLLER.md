# Batch29 Synthesis For Controller

Date: 2026-06-04 23:10 +04:00.
Evidence class: STATIC_FILESYSTEM + HUMAN_VISUAL_REVIEW + STATIC_TOOL_RUN.

## Verdict

No Unity visual acceptance exists.

Latest complete six-route packet remains `1474` and is rejected. Latest raw visual event `1912` is also rejected and adds a scene/proof-hygiene blocker.

## Recovery Gate Result

- Authority re-read: `AGENTS.md`, `HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md`, `HECTON8_ORCHESTRATOR.md`, current orchestration day tail, newest `UNITY_OWNER_*`, `TASTE.md`, `VISION_LOCKS.md`, `quality.md`, `water.md`, `rendering.md`, `terrain.md`, `celestial.md`, relevant render/VFX/evidence mandates.
- Newest Unity-owner steer: `Docs/Orchestration/UNITY_OWNER_STEER_20260604_1912_REJECT_QUARANTINE_SCRIPT.md`.
- Newest visual events:
  - `Docs/Screenshots/MCP/h8_1912_surface_edit_main.png`;
  - `Docs/Screenshots/MCP/h8_1912_surface_after_quarantine_b.png`.
- Proof packets under `Docs/Screenshots/HectonProofPackets`: none found.
- Current process sample: no Unity/dotnet/csc/ILPP process in the last controller check.
- Latest Unity log remains dirty and cannot certify clean proof.

## ProofGate / Watchdog

Command:

`python -m unittest discover -s Tools\ProofGate -p test_*.py`

Result:

- `Ran 21 tests`
- `OK`

Command:

`python Tools\ProofGate\unity_process_proof_watchdog.py --repo-root C:\hades\Hecton8 --strict --json-out Docs\Reports\Batch29\UnityProcessProofWatchdog_latest.json --md-out Docs\Reports\Batch29\UnityProcessProofWatchdog_latest.md`

Result:

- `STATIC_BLOCKED`
- `DIRTY_LOG_TOKENS_FOUND`
- `RAW_PNG_SET_NO_MANIFEST`

## 1912 Visual Rejection

`Docs/Screenshots/MCP/h8_1912_surface_edit_main.png` and `Docs/Screenshots/MCP/h8_1912_surface_after_quarantine_b.png` fail the surface/photic floor:

- black primitive foreground boulders/slabs dominate the camera;
- water is a flat dark green mesh/sheet with weak breakup;
- yellow/green left foreground artifact is visible;
- shoreline close wet-contact is absent;
- believable foam is absent;
- visible caustic proof is absent;
- terrain still reads as shell/black/weak;
- Aegir remains a muddy impostor-like sphere;
- no underwater volume, particles, 0-5 m route, 20-50 m route, or return cue exists.
- the after-quarantine raw capture is visually the same rejected composition and does not prove cleanup or improvement.

Compared against local direction-only frames (`1428_surface_game_cloud_deck_pass12`, `1428_sky_foam_caustics_pass_game`, `h8_1473_mainrt_crest_foam_shoreline`, `h8_1908_surface_runtime_ui_on`), `1912` regresses composition and proof hygiene. Those older frames are also rejected, but they show a cleaner surface/waterline target than the 1912 diagnostic camera.

Detailed compare:

- `Docs/Reports/Batch29/BATCH29_1912_VISUAL_REJECTION_AND_REFERENCE_COMPARE.md`

## 1912 Process Blocker

The `UnityCapture_1912_surface_after_quarantine*` logs still contain editor import/refresh/shutdown noise, MCP server shutdown noise, memory leak telemetry, and StackAllocator warning. They cannot certify a clean proof window.

`Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs` is an untracked one-off editor script under `Assets`.

It is not only a capture helper. `QuarantineSurfaceRejectsAndExit()` opens `Assets/_Project/Scenes/02_HECTON_WORLD.unity`, disables renderers by name, marks the scene dirty, saves it, then exits.

Current static evidence:

- `git status` shows `Assets/_Project/Scenes/02_HECTON_WORLD.unity` modified.
- `git diff --stat -- Assets/_Project/Scenes/02_HECTON_WORLD.unity` shows `93725` changed lines: `68153 insertions`, `25572 deletions`.
- `Docs/Screenshots/MCP/h8_1912_surface_quarantine.txt` lists disabled/debug/rejected renderer names including cyan depth lanes, noir slabs, broken foam sheets, black boulders, photic rock garden, caustic sheet, and flat green surface haze sheet.

This scene diff is not accepted cleanup yet. It is `PENDING REVIEW`.

## Required Controller Direction

1. Do not accept `1912` as progress proof.
2. Do not run another one-off capture script under `Assets`.
3. Do not save diagnostic quarantine changes into production scene without owner-correct review, exact object list, reason, rollback path, and visual proof.
4. Treat the scene diff as a blocker for the Unity owner.
5. Keep independent no-Unity fronts moving:
   - scene mutation diff audit;
   - manifest-bound proof harness contract;
   - reference-direction visual matrix;
   - water/foam/caustic material route audit;
   - shoreline/terrain/generated asset route;
   - Aegir/sky asset route.
6. Next accepted candidate must be `1475+` manifest-bound proof packet under `Docs/Screenshots/HectonProofPackets/...`.

## Current Front

Portfolio orchestration around rejected Unity visual/runtime proof.

Unity-owner lane remains the separate VS Code Codex GUI thread `Продолжить работу по логам`; the 1912 steer file is created only and GUI delivery is not claimed without screenshot proof.

## Residual Risk

- The scene may currently contain diagnostic renderer disables from `H8VisualProofCapture1912.cs`.
- Unity log is dirty.
- No current runtime clean route proof exists after the scene mutation.
- Existing source patches for leak/registry/proof tooling remain `PENDING UNITY VERIFICATION`.
