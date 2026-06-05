# STEER_BATCH28_SYNTHESIS

Target: `Продолжить работу по логам` Unity owner.
Date: 2026-06-04 22:00 +04:00.
Source: `Docs/Reports/Batch28/BATCH28_SYNTHESIS_FOR_UNITY_OWNER.md`.

## Immediate Facts

`1474` remains `REJECTED`.

`h8_1908_surface_runtime_ui_on.png` is a single raw surface screenshot. It is not a proof packet.

Controller source patches are still `PENDING UNITY VERIFICATION`:

- `SeamGapDitherRenderer` buffer lifecycle cleanup;
- `GameBootstrapper` scene-gate publication of `GlobalRegistry.UnderwaterVisuals`;
- `HectonUnderwaterVisuals` removal of arbitrary runtime `OnEnable()` self-publication.

## Required Order

1. Let Unity settle before proof or compile claims.
2. Verify controller patches with a fresh import/reload/play-exit log:
   - no compile errors;
   - no `SeamGapDitherRenderer.EnsureBuffers` / `GraphicsBuffer:.ctor` persistent leak stack;
   - no ready-lock rejection for underwater visuals;
   - `GlobalRegistry.UnderwaterVisuals` points to the accepted scene owner.
3. Build or use an owned proof harness. Raw MCP screenshot names are rejected.
4. Produce `1475` or newer under:
   - `Docs/Screenshots/HectonProofPackets/h8_1475_{session_id}/`
5. Include:
   - `manifest.json`;
   - `manifest.sha256`;
   - copied `UnityEditor_{packet_id}_{session_id}.log`;
   - six production screenshots under `screenshots/`.
6. Run the new static gate before claiming ready for review:

```text
python Tools/ProofGate/validate_proof_packet.py --packet-root Docs/Screenshots/HectonProofPackets/h8_1475_{session_id} --packet-id h8_1475 --session-id {session_id} --expected-quality qNNN --strict
```

The gate passing only means static packet review may start. It is not visual acceptance.

## Mandatory 1475 Views

- `01_surface_coast_aegir_ui_off.png`
- `02_shoreline_close_1m.png`
- `03_underwater_0_5m.png`
- `04_underwater_20_50m_route.png`
- `05_aegir_celestial_long.png`
- `06_regression_low_oblique.png`

## Route Requirements

Underwater:

- `underwater_0_5m` must prove camera depth `>= 0.25m` and `<= 5.0m`.
- `underwater_20_50m_route` must prove camera depth `>= 20.0m` and `<= 50.0m`.
- Manifest must bind water level, exact depth, underwater active state, depth zone min/max/hash, route owner, route anchor, and camera source.
- Image must show water volume, particles/detail, depth structure, and route/return cue. Surface/coast/Aegir horizon dominance is rejected.

Shoreline:

- `shoreline_close_1m` must show real 1 m wet-contact proof.
- Needs organic foam following shoreline shape, wet rock/material response, shell/sand/sediment scale cue, shallow depth falloff.
- Generic transparent foam ribbon alone is not proof.

Sky/Aegir:

- Primary route is `PrimarySunDiscOwner=SkyMaterial`.
- Do not activate or wire `SURFACE_LOW_SUN_DISC_1428` as the primary sun fix.
- Manifest must disclose sky material/shader, Aegir material/shader/textures, runtime sky/Aegir values, texture residency, and mesh sun not-required state.
- Aegir crop must reject rim, veil, seam, sticker, stripe, and dirty-noir hiding.

Quality:

- Record `GlobalQualityWeight` as a continuous float and `qNNN` derived label.
- No binary low/high switch.

Log:

- Clean post-capture log window must be newer than the final screenshot.
- Reject compile/import/domain reload/ILPP/MCP transport/leak/ready-lock noise in the proof window.

## Static Gate Control Result

The controller ran the new packet gate against the current raw MCP folder:

```text
REJECTED_STATIC_GATE
RAW_PNG_SET
```

Reports:

- `Docs/Reports/Batch28/ProofPacketGate_h8_1474_mcp.json`
- `Docs/Reports/Batch28/ProofPacketGate_h8_1474_mcp.md`

This is expected and confirms `1474` remains blocked before visual review.

## Reject If

- screenshots are raw MCP files without manifest;
- any required production view is missing or substituted by diagnostic overlay;
- underwater filenames lack depth/underwater owner predicates;
- shoreline lacks wet-contact/foam/material proof;
- foam/caustics are cheap global ribbons/sine sheets with no owner/depth/receiver proof;
- Aegir or sky relies on `SURFACE_LOW_SUN_DISC_1428`;
- log is dirty or stale;
- any source patch is called fixed without fresh Unity runtime evidence.
