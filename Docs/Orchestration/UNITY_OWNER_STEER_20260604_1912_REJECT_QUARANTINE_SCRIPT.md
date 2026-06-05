# UNITY OWNER STEER 2026-06-04 1912 REJECT QUARANTINE SCRIPT

Target thread: `Продолжить работу по логам`.
Delivery status: file created only. Do not claim GUI delivery without screenshot proof of the correct thread.

## Immediate Verdict

`h8_1912_surface_edit_main.png` is `REJECTED`.

Do not treat it as progress proof.

## Why

- It is a raw editor surface screenshot, not a manifest-bound proof packet.
- It has no six required views, no checksums, no camera/depth/quality/toggles/log manifest, and no clean log window.
- Visually it is still below floor:
  - black primitive foreground boulders/slabs;
  - flat dark green water mesh/sheet;
  - yellow/green artifact on left foreground;
  - no close shoreline wet-contact proof;
  - no believable foam;
  - no visible caustic proof;
  - terrain still shell/weak;
  - Aegir still dirty/impostor-like;
  - no underwater volume, particles, depth route, or return cue.

## Serious Process Defect

`Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs` is not just capture.

`QuarantineSurfaceRejectsAndExit()`:

- opens `Assets/_Project/Scenes/02_HECTON_WORLD.unity`;
- disables renderers by name;
- marks scene dirty;
- calls `EditorSceneManager.SaveScene(scene)`.

That permanently mutated the scene and generated a massive scene diff.

This is not an accepted proof harness. It is a scene mutation helper. Treat it as `PENDING REVIEW`, not a fix.

## Required Now

1. Stop using 1912 raw capture as proof.
2. Review the 1912 scene diff before any new acceptance run.
3. If those 23 renderer disables are intended, convert them into an owner-correct scene cleanup pass with exact object list, reason, rollback path, and visual proof.
4. If they are only diagnostic quarantine, do not save them into production scene.
5. Move future proof harness work toward a reusable manifest-bound harness under the agreed route:
   - `Docs/Screenshots/HectonProofPackets/h8_1475_{session}/`
   - `manifest.json`
   - `manifest.sha256`
   - copied Unity log
   - six production screenshots under `screenshots/`
   - static gate run.
6. Do not put screenshot/log junk under `Assets`.

## Visual Target Reminder

Surface and photic routes must be bright, beautiful, readable, detailed, and premium. Subnautica-level is the floor.

The next packet must visibly prove:

- real waterline composition;
- organic shoreline foam;
- wet rock/material response;
- shallow caustics with believable receiver/light route;
- underwater 0-5 m volume and particles;
- underwater 20-50 m route depth and return cue;
- Aegir/sky texture quality without muddy impostor artifacts;
- no foreground black placeholder shells.

## Controller Evidence

Full compare note:

- `Docs/Reports/Batch29/BATCH29_1912_VISUAL_REJECTION_AND_REFERENCE_COMPARE.md`

Current watchdog:

- `Docs/Reports/Batch29/UnityProcessProofWatchdog_latest.md`
- `Docs/Reports/Batch29/UnityProcessProofWatchdog_latest.json`

Current static gate tests:

- `python -m unittest discover -s Tools\ProofGate -p test_*.py`
- Result: `Ran 21 tests`, `OK`.

