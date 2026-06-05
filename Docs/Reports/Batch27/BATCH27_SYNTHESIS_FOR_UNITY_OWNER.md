# Batch27 Synthesis For Unity Owner

Date: 2026-06-04 21:24 +04:00.
Scope: runtime proof recovery after rejected `1474` and Batch26 synthesis.

## Current Verdict

No visual/runtime acceptance is possible yet.

`1474` remains rejected. No `1475` packet or proof manifest exists. The latest inspected Unity log is dirty and cannot certify proof.

One direct source patch was applied by the controller:
- `Assets/_Project/Scripts/SeamGapDitherRenderer.cs`

Patch status:
- `PENDING UNITY VERIFICATION`

Do not claim the leak fixed until Unity import/compile and fresh reload/play-exit logs prove it.

## 2701 SeamGapDitherRenderer Leak

Finding:
- `SeamGapDitherRenderer` allocated six `GraphicsBuffer`s in `EnsureBuffers()`.
- Prior source released them only in `OnDestroy`.
- `OnDisable()` only unregistered dispatcher/listener, so reload/play-exit could leave live buffers.

Controller patch:
- `OnDisable()` now unregisters, clears pending draw/debug state, and releases buffers.
- `EnsureBuffers()` now treats all six buffers as one coherent allocation set.
- Buffer readiness/release now uses `GraphicsBuffer.IsValid()`.

Proof required:
- Unity import/compile success.
- Fresh reload/play-exit log with no `SeamGapDitherRenderer.EnsureBuffers`, `GraphicsBuffer:.ctor`, or `Persistent allocates` leak stack from this renderer.
- Visual regression proof that seam/root dither still renders after re-enable.

Remaining warning:
- `1474b` also contained non-seam leak stacks such as `WeatherEvents`, `WorldProceduralScatterDirector`, and Crest-related stacks. Clearing seam leak alone may not make the proof log clean.

## 2702 Underwater Registry Publication

Finding:
- `HectonUnderwaterVisuals` still self-publishes to `GlobalRegistry` from `OnEnable()`.
- That path can hit ready-lock outside the scene publication gate.
- `Start()` retries dependencies, but not service publication.

Owner-correct route:
- `GameBootstrapper` / scene activation owns `UnderwaterVisualsRuntime` service publication.
- Use the existing scene runtime publication gate.
- Keep `HectonUnderwaterVisuals` as underwater presentation owner, not global publication owner.

Rejected shortcuts:
- no ready-lock bypass;
- no `Start()` retry;
- no per-frame registry retry;
- no reliance on `[DefaultExecutionOrder]` as proof.

Proof required:
- clean log with no ready-lock rejection;
- one active enabled underwater owner;
- registry slot bound to the scene owner;
- underwater proof manifest fields for owner, depth, material, quality, and log state.

## 2703 Owned Capture Manifest Harness

Finding:
- Current first-party and MCP screenshot routes emit raw PNGs.
- They do not write HECTON proof packets.
- There is no owned manifest writer/checksum/log binder for route, depth, quality, UI state, and clean-log proof.

Required harness pieces:
- runtime proof contracts and read models;
- public side-effect-free proof snapshots from `HectonUnderwaterVisuals`;
- predicate-grade depth proof from `DepthZoneDirector`;
- owned route capture rig/view IDs;
- editor harness;
- manifest writer;
- SHA256/dimension/timestamp reader;
- clean log window validator;
- route predicate evaluator.

Recommended output root:
- `Docs/Screenshots/HectonProofPackets/h8_1475_{session_id}/`

No `1475` can be accepted without:
- six production route-correct views;
- diagnostic route overlay view;
- manifest;
- clean 60 second post-capture log gate.

## 2704 Shoreline Texture Generation QA

Finding:
- No current Gemini shoreline/photic source is ready for derivation, Unity import, production material binding, TerrainLayer replacement, Crest foam binding, or route proof.

Future generation contract:
- source root: `Docs/GeneratedAssets/Gemini/Outputs/Batch27/2704/`
- audit root: `Docs/GeneratedAssets/Gemini/Audit/Batch27/2704/`
- every source requires sidecar manifest, SHA256, static intake, 2x2 preview, 3x3 preview, manual review, and explicit status label.

Unity import remains blocked until:
- status is `READY_FOR_UNITY_IMPORT`;
- derived stack exists;
- import settings plan exists;
- target material/TerrainLayer names exist;
- rollback plan exists;
- quiet Unity owner slot exists.

Do not write new generated sources to `Assets/**`.

## 2705 Aegir / Sky Owner Route

Recommended primary sun route:
- sky-material sun disc owned by `Mat_HectonSky.mat` / `Hecton_AlienSky_Master.shader`;
- driven by `HectonCelestialEngine`;
- routed through `AtmosphereDirector`.

Rejected quick fix:
- do not activate `SURFACE_LOW_SUN_DISC_1428`;
- do not assign it to `HectonCelestialEngine.sunVisualTransform` as a quick owner fix.

Reason:
- current source already treats atmosphere-present route as sky-owned primary sun;
- `SURFACE_LOW_SUN_DISC_1428` is inactive, renderer-disabled, and flat/untextured;
- enabling it creates a second sun truth owner and will not meet the visual floor.

Required source/proof metadata:
- `PrimarySunDiscOwner=SkyMaterial`

Required cleanup:
- `HectonUnderwaterVisuals` sun visual validation/warnings must be route-aware so sky-material ownership does not create false unresolved-sun blockers.

Proof required:
- fresh Aegir long and crop views;
- manifest records sky material/shader GUIDs, Aegir material/shader GUIDs, runtime sky/Aegir values, texture residency, camera/FOV, quality, log path, and checksums;
- crop must check rim, veil, seam, sticker, and stripe artifacts.

## Required Unity Owner Order

1. Let Unity settle. Do not capture during import/compile/domain reload/MCP startup.
2. Verify the `SeamGapDitherRenderer` patch:
   - import/compile clean;
   - reload/play-exit clean for this renderer;
   - visual regression screenshot.
3. Fix `HectonUnderwaterVisuals` publication ownership:
   - move global service publication to bootstrap/scene activation gate;
   - reject late self-publication.
4. Implement minimal proof snapshot/read-model support needed by the capture harness:
   - underwater proof snapshot;
   - depth proof snapshot;
   - quality/render-scale snapshot binding;
   - route/view-id rig.
5. Freeze `PrimarySunDiscOwner=SkyMaterial`.
6. Make sun-visual warning paths route-aware under sky ownership.
7. Keep generated texture work staged under docs until QA promotes a complete material stack.
8. Only then produce `1475` through an owned harness, not raw MCP filename screenshots.

## Non-Acceptance Rules

- A patched source file is not proof.
- A clean static report is not runtime proof.
- A PNG set without manifest is rejected.
- A log from an older Unity session is rejected.
- A surface-looking underwater view is rejected.
- A shoreline view without 1 m contact/wetness/foam/material scale proof is rejected.
- Aegir/celestial proof that hides artifacts with crop, darkness, haze, or bloom is rejected.

## Source Reports

- `Docs/Reports/Batch27/2701_SEAMGAP_DITHER_GRAPHICSBUFFER_LEAK_AUDIT.md`
- `Docs/Reports/Batch27/2702_UNDERWATER_REGISTRY_PUBLICATION_ROUTE_AUDIT.md`
- `Docs/Reports/Batch27/2703_OWNED_CAPTURE_MANIFEST_HARNESS_SPEC.md`
- `Docs/Reports/Batch27/2704_SHORELINE_TEXTURE_GENERATION_QA_ROUTE.md`
- `Docs/Reports/Batch27/2705_AEGIR_SKY_OWNER_VISUAL_POLISH_ROUTE_AUDIT.md`
- `Docs/Reports/Batch27/BATCH27_CONTROLLER_TRACKER_INTERIM.md`
