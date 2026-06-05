# Unity Owner Dialogue Review - 2026-06-05

Status: `STATIC_DIALOGUE_REVIEW / CONTROLLER_EVIDENCE`
Evidence class: `USER_ATTACHMENT + STATIC_SOURCE + STATIC_DOC + STATIC_IMAGE_REJECTION`

Attachment reviewed:

- `C:\Users\danat\.codex\attachments\2f281a38-64e6-4996-8703-bf2ace6239e8\pasted-text.txt`

This file records controller facts only. The dialogue is not Unity proof, visual acceptance, compile proof, profiler proof, GC proof, or approval to mutate scenes/materials.

## Controller Verdict

The Unity-owner lane spent too much time treating the current surface failure as a color/haze/material symptom. The actual blocker is larger:

- no accepted surface-water presentation route;
- no proven active production player/HUD/tool route;
- no canonical h8_1475 proof packet;
- diagnostic screenshots still show slab water, detached terrain, black gaps, weak shoreline contact, and green camouflage.

The green surface-water/haze direction is rejected as a final route. It can remain diagnostic evidence only.

## Useful Facts From The Dialogue

- MCP HTTP bridge repeatedly dropped during screenshots and A/B probes.
- Unity Editor closed or became unstable during parts of the A/B cycle.
- Batch capture can produce PNGs, but prior `-nographics`/URP capture produced tiny/invalid black output and is not automatically trustworthy.
- The worker eventually identified the correct strategic error: patching symptoms instead of restoring an authoritative surface route.
- Current visible route uses `Assets/Crest/Crest/Materials/Ocean.mat` in static prior reports; `MAT_H8_SurfaceCrestOcean_1428.mat` is a candidate only and has known overdrive/acid-water risk.
- Historical reference direction points at MapMagic + Crest + sky material route, not a green overlay or temporary card.
- User-visible reference target is cyan/blue bright surface water, foam/wet shoreline contact, readable terrain/geology, vegetation/route density, and Aegir/sky integration.

## Source Review Added After Dialogue

Target:

- `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs`

Static findings:

- `CaptureSurfaceWaterRecoveryProbeAndExit()` writes diagnostic `h8_1914_surface_water_recovery_probe` output and tags metadata as `surface_water_recovery_probe_editor_only_unsaved`.
- `CaptureSurfaceCrestRecoveryProbeAndExit()` changes `Crest.OceanRenderer` serialized fields through `SerializedObject.ApplyModifiedPropertiesWithoutUndo()`.
- The Crest recovery probe has no visible restore path.
- `QuarantineSurfaceRejectsAndExit()` is an actual scene mutation path because it marks and saves the scene.

Result:

- `H8VisualProofCapture1912.cs` is a diagnostic rejection runner, not a canonical proof runner.
- Any `h8_1914_*` output from this runner must remain rejected/diagnostic unless a future owner proves an explicitly no-mutation capture route.

Linked static risk review:

- `Docs/AssetAudit/H8_VISUAL_PROOF_CAPTURE_1912_STATIC_RISK_REVIEW_20260605.md`

## New Crest Probe Rejection

Fresh evidence reviewed after the dialogue:

- `Docs/Reports/UnityCaptureSurfaceCrestActualTerrainProbe_20260605_231353.log`
- `Docs/Screenshots/MCP/h8_1914_surface_crest_recovery_probe.png`
- `Docs/Screenshots/MCP/h8_1914_surface_crest_recovery_probe.txt`

Controller verdict:

- The run used `H8VisualProofCapture1912.CaptureSurfaceCrestRecoveryProbeAndExit`, already classified as diagnostic-only.
- Metadata says `captureTruth=surface_actual_terrain_crest_recovery_probe_editor_only_unsaved`.
- The capture includes temporary horizon haze material `H8_TEMP_SurfaceHorizonHazeProbe_1428`.
- The image still shows slab water, black detached island underside, green horizon/haze line, and weak Aegir/sky integration.
- This is not h8_1475, not no-mutation proof, not product proof, and not a pass.

ProbeB/ProbeC repeat:

- `Docs/Reports/UnityCaptureSurfaceCrestActualTerrainProbeB_20260605_232055.log` and `Docs/Reports/UnityCaptureSurfaceCrestActualTerrainProbeC_20260605_232834.log` repeat the same diagnostic runner path.
- Both write the same `Docs/Screenshots/MCP/h8_1914_surface_crest_recovery_probe.*` filenames, overwriting prior evidence.
- Later overwritten PNG hash after the newest observed `00:39` artifact: `3BB21F415E0499393FB9DD5445ECDE01AFEB9A33CCD00C3CE409BF3C1BA12DCC`.
- Later overwritten metadata hash after the newest observed `00:39` artifact: `D3ED28BE2D128835524076F3FEED76B581EBC48718A3D5F1D79E8B8835FDEBB7`.
- The latest image adds a visible rectangular terrain/material patch in the lower right. This is a stronger rejection, not progress.
- Metadata shows active MapMagic `Main Terrain` with flat `size=(15000.00, 0.00, 15000.00)` plus first-party terrain shell and temporary haze. The worker is still proving a mixed diagnostic setup, not an authoritative route.

## Added Dialogue Critique

The useful lesson from the pasted Unity-worker dialogue is not that one color choice failed. The worker repeatedly treated the surface failure as a local material/color/haze problem and kept running temporary cards, green overlays, and screenshot probes while the real route was still unproven.

Controller classification:

- `surface_clean_ab6`, green haze, temporary water-skin cards, and any `h8_1914_*` capture are diagnostic rejects, not progress markers.
- The current visual failure is systemic: missing readable water body, black/undercut terrain, rectangular slab/patch artifacts, weak shoreline contact, unstable proof tooling, and no active player/HUD/tool proof.
- Future Unity lane must start from authoritative route readback: MapMagic terrain state, Crest ocean renderer/material/source asset, sky/Aegir/cloud route, active player, active HUD/projection, active input/camera/tool, dirty-state audit, and console.
- If the owner cannot prove the active route, the correct action is not another color pass. It is recovery/replacement of the owner route with no-mutation proof and reference-matched acceptance criteria.

## Rejected Directions

- Do not pursue green haze as root fix.
- Do not use temporary water-skin cards as production water proof.
- Do not assign `MAT_H8_SurfaceCrestOcean_1428.mat` blindly. It is not active route proof and has repeated overdrive risk in prior reports.
- Do not accept landscape-only screenshots while active player/HUD/tool/input route remains unproven.
- Do not use `CaptureSurfaceCrestRecoveryProbeAndExit()` for h8_1475 no-mutation proof.
- Do not place rocks, flora, coral, or decoration to hide broken water/terrain/sky composition.

## Required Next Unity Lane

1. Run `RUNTIME_OWNER_05_MCP_GATE_AND_READBACK_RECOVERY_PACKET.md` only when process gate is green.
2. Prove active player, HUD, input, camera, tool, and dirty-state routes before any h8_1475 visual acceptance attempt.
3. Run `ASSET_OWNER_37_H8_1475_ANTI_FALSE_PROOF_ALIGNMENT_PACKET.md` as a hard gate against scenic fake proof.
4. Run `ASSET_OWNER_36_H8_1475_PROOF_EXECUTION_PACKET.md` only after player/HUD/tool route is known.
5. Surface repair owner must recover or replace the authoritative surface route:
   - active OceanRenderer material and Crest source route;
   - ocean extents/horizon without slab edges;
   - terrain/wet-rock material and lighting;
   - foam/contact/waterline evidence;
   - Aegir/sky/cloud integration;
   - no temporary green overlay as acceptance proof.

## Low / Middle / High / Ultra Consequences

- Low: bright readable cyan/blue water, shoreline contact, terrain silhouette, Aegir, and player HUD/tool route must still pass. No ugly green mode.
- Middle: expected route must show credible wet shore, terrain material breakup, photic water volume, and production HUD/interaction.
- High: spend budget on richer water normals, foam/contact masks, terrain materials, route vegetation/geology, and sky/Aegir detail.
- Ultra: capture-grade density and sky/water polish only after the same gameplay truth owner passes. No alternate scenic camera truth.

Final status: `REJECTED AS ACCEPTANCE / USEFUL AS DIAGNOSTIC EVIDENCE`.
