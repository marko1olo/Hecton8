# STEER_BATCH27_SYNTHESIS

Target: `Продолжить работу по логам` Unity owner.
Date: 2026-06-04 21:24 +04:00.
Source: `Docs/Reports/Batch27/BATCH27_SYNTHESIS_FOR_UNITY_OWNER.md`.

## Immediate Facts

`1474` remains rejected. No `1475` packet or manifest exists.

Controller applied one source patch:
- `Assets/_Project/Scripts/SeamGapDitherRenderer.cs`

Patch is not proof. Treat it as `PENDING UNITY VERIFICATION`.

## Required Order

1. Let Unity settle.
2. Verify `SeamGapDitherRenderer` patch:
   - import/compile clean;
   - fresh reload/play-exit log;
   - no `SeamGapDitherRenderer.EnsureBuffers`, `GraphicsBuffer:.ctor`, or `Persistent allocates` stack from this renderer;
   - one visual regression proof that seam/root dither still renders.
3. Fix `HectonUnderwaterVisuals` publication ownership:
   - stop global service self-publication from arbitrary `OnEnable()`;
   - publish through `GameBootstrapper` / scene activation using existing scene runtime publication gate;
   - no ready-lock bypass, no `Start()` retry, no per-frame retry.
4. Build the owned proof harness route:
   - manifest writer;
   - checksum/timestamp/dimension reader;
   - clean log window validator;
   - route predicate evaluator;
   - public proof snapshots from underwater/depth owners;
   - output under `Docs/Screenshots/HectonProofPackets/h8_1475_{session_id}/`.
5. Freeze celestial route:
   - `PrimarySunDiscOwner=SkyMaterial`;
   - primary route is `Mat_HectonSky.mat` / `Hecton_AlienSky_Master.shader`;
   - do not activate `SURFACE_LOW_SUN_DISC_1428` as a quick fix;
   - make `HectonUnderwaterVisuals` unresolved-sun warnings route-aware.
6. Keep generated texture work out of `Assets/**` until QA promotes a complete stack. Current Gemini shoreline/photic sources are not ready for import.
7. Produce `1475` only through the owned harness, with manifest and clean 60 second post-capture log gate.

## Reject If

- screenshots are raw MCP filenames without manifest;
- log is dirty or from an older Unity session;
- underwater routes are surface-looking again;
- shoreline lacks 1 m wet-contact/foam/material proof;
- Aegir/celestial crop shows rim, veil, seam, sticker, stripe, or dirty-noir hiding;
- any claim says the seam leak is fixed before fresh Unity reload/play-exit proof.
