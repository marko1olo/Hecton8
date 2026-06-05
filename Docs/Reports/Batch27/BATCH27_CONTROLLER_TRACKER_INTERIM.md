# Batch27 Controller Tracker - Interim

Date: 2026-06-04 21:15 +04:00.
Controller: local orchestrator.
Status: active report-only worker wave.

## Active Workers

- `2701_SEAMGAP_DITHER_GRAPHICSBUFFER_LEAK_AUDIT` -> Harvey / `019e93a0-2462-76f3-9514-c84c34b22cde` -> COMPLETE + CONTROLLER PATCH APPLIED
- `2702_UNDERWATER_REGISTRY_PUBLICATION_ROUTE_AUDIT` -> Bernoulli / `019e93a0-24ba-7f83-b33a-571daf38ca60` -> COMPLETE
- `2703_OWNED_CAPTURE_MANIFEST_HARNESS_SPEC` -> Hume / `019e93a0-250c-7600-9776-1b517a0ac3fc` -> COMPLETE
- `2704_SHORELINE_TEXTURE_GENERATION_QA_ROUTE` -> Parfit / `019e93a0-256c-75d0-8e33-08e3f49685e8` -> COMPLETE
- `2705_AEGIR_SKY_OWNER_VISUAL_POLISH_ROUTE_AUDIT` -> Noether / `019e93a0-25ef-7311-a8d5-60a82eb6591d` -> COMPLETE

All five tasks are report-only. They must not launch Unity, Play Mode, dotnet build, process kills, browser generation, asset imports, or `Assets/**` edits.

## Current Proof State

- Newest complete MCP screenshot packet: `1474`.
- Current verdict: `REJECTED`.
- No `1475` screenshot or manifest exists.
- Latest `Editor.log` timestamp inspected: `2026-06-04 21:09:40`.
- Latest process sample: no Unity, compiler, ILPP, shader compiler, dotnet, csc, MSBuild, or VBCSCompiler process visible.
- The latest `Editor.log` is still dirty: licensing error, reloads, Asset Pipeline Refresh, `CompileScripts`, MCP errors/warnings, and import worker activity for core visual files.

## Existing Hard Gates From Batch26

1. No proof acceptance until a same-session manifest binds screenshots to route/depth/camera/quality/material/log data.
2. No proof acceptance while `HectonUnderwaterVisuals` can hit ready-lock service publication rejection.
3. No proof acceptance while `SeamGapDitherRenderer.EnsureBuffers()` Persistent `GraphicsBuffer` leak is present or unverified.
4. No shoreline acceptance while active terrain depends broadly on rejected wet basalt 1428 and lacks accepted PBR material families.
5. No underwater acceptance while volume/detail owner state is missing or unproven and underwater material caustics remain zero in static route.
6. No Aegir/celestial acceptance until sun ownership is explicit and proof shows a premium route, not inactive mesh ambiguity.

## Controller Direct Patch - SeamGapDitherRenderer

File touched:
- `Assets/_Project/Scripts/SeamGapDitherRenderer.cs`

Patch status:
- `PENDING UNITY VERIFICATION`

Reason:
- `2701` found six `GraphicsBuffer` allocations in `EnsureBuffers()` but release only through `OnDestroy`.
- `OnDisable()` now unregisters, clears pending draw/debug state, and releases owned buffers.
- `EnsureBuffers()` now treats the six buffers as one coherent allocation set, releases stale/invalid buffers first, then recreates all six together.
- Buffer readiness and release now use `GraphicsBuffer.IsValid()`.

Verification already done:
- `git diff --check -- Assets/_Project/Scripts/SeamGapDitherRenderer.cs` passed with only the existing CRLF warning.
- Static search confirmed `GraphicsBuffer.IsValid()` is already used elsewhere in project code.

Verification still required:
- Unity import/compile check.
- Fresh reload/play-exit log with no `SeamGapDitherRenderer.EnsureBuffers`, `GraphicsBuffer:.ctor`, or `Persistent allocates` leak stack from this renderer.
- Visual regression proof that seam/root dither still renders after component re-enable.

## Do Not Count As Progress

- New PNG names without manifest.
- Static YAML values without runtime proof.
- Material strength increases without visual proof.
- Raw-enabling haze, slabs, pressure lid, curtain, or occlusion helpers.
- Importing more generated sources into `Assets/**` before static intake and manual tile review.
- A log tail older than the final screenshot or from a previous Unity session.

## Next Controller Action

Wait for Batch27 reports. After the first completion, read the report, compare against Batch26 synthesis, and update Unity-owner steer only if it adds a precise owner-correct correction or rejects a dangerous shortcut.

## Completed Finding - 2702

`Docs/Reports/Batch27/2702_UNDERWATER_REGISTRY_PUBLICATION_ROUTE_AUDIT.md`

Actionable finding:
- `HectonUnderwaterVisuals` still self-publishes to `GlobalRegistry` from `OnEnable()`.
- Owner-correct route is `GameBootstrapper` / scene activation publication, using the existing scene runtime publication gate.
- Do not bypass ready-lock; do not add `Start()`/per-frame retry; do not rely on `DefaultExecutionOrder`.
- Existing scene has one active owner, but runtime publication remains PENDING VERIFICATION.

## Completed Finding - 2704

`Docs/Reports/Batch27/2704_SHORELINE_TEXTURE_GENERATION_QA_ROUTE.md`

Actionable finding:
- No current Gemini shoreline source is ready for derivation, Unity import, production material binding, TerrainLayer replacement, Crest foam binding, or route proof.
- Future generation output root is `Docs/GeneratedAssets/Gemini/Outputs/Batch27/2704/`, with audit output under `Docs/GeneratedAssets/Gemini/Audit/Batch27/2704/`.
- Every source needs sidecar manifest, SHA256, static intake, 2x2 and 3x3 preview, manual review, and explicit status label before derivation.
- Unity import is a separate owner slot and requires `READY_FOR_UNITY_IMPORT`, derived stack, import settings plan, material names, rollback plan, and quiet Unity window.

## Completed Finding - 2703

`Docs/Reports/Batch27/2703_OWNED_CAPTURE_MANIFEST_HARNESS_SPEC.md`

Actionable finding:
- Current first-party and MCP screenshot paths are raw image emitters, not proof packet writers.
- Proposed harness needs runtime contracts, proof read models, editor harness, manifest writer, log window validator, SHA256/dimension reader, route predicate evaluator, and packet output under `Docs/Screenshots/HectonProofPackets/h8_1475_{session_id}/`.
- `HectonUnderwaterVisuals` needs a side-effect-free public proof snapshot.
- `DepthZoneDirector` needs predicate-grade depth proof beyond `CurrentZone`.
- No `1475` can be accepted without clean runtime capture and a 60s post-capture log gate.

## Completed Finding - 2705

`Docs/Reports/Batch27/2705_AEGIR_SKY_OWNER_VISUAL_POLISH_ROUTE_AUDIT.md`

Actionable finding:
- Recommended primary route is sky-material sun disc owned by `Mat_HectonSky.mat` / `Hecton_AlienSky_Master.shader`, driven by `HectonCelestialEngine` through `AtmosphereDirector`.
- `SURFACE_LOW_SUN_DISC_1428` is inactive, renderer-disabled, flat/untextured, and must not be activated as a quick fix.
- `PrimarySunDiscOwner=SkyMaterial` must be recorded in source/proof metadata.
- `HectonUnderwaterVisuals` sun-visual warnings must become route-aware under sky-material ownership.
- Fresh Aegir long/crop proof must inspect rim, veil, seam, sticker, and stripe artifacts with material/runtime values in the manifest.
