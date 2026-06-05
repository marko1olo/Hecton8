# H8 1475 Proof Tool Integrity Synthesis - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_SOURCE`, `STATIC_DOC`.
Subagent source: Godel and Bacon proof-tool integrity audits.

No Unity run, Play Mode, scene save, prefab save, material save, import, profiler, Frame Debugger, project-setting mutation, Addressables build, or runtime code mutation was performed.

## Current Verdict

`Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs` is rejected for canonical `h8_1475` acceptance. It can produce diagnostic rejection screenshots only. It must not be used to promote the game state, because current capture paths either lack the `h8_1475` packet contract or mutate editor scene/render state before capture.

First-20 route impact: this blocks false promotion of scenic water/shore/sky screenshots while active player, HUD, movement, tool route, no-mutation readback, and visual floor are still unproved.

## Exact Static Anchors

| Anchor | Finding |
|---|---|
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:20` | `CaptureRoot` writes to raw `Docs/Screenshots/MCP`, not `Docs/Screenshots/HectonProofPackets/h8_1475_*`. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:28` | Public methods emit `h8_1912`, `h8_1913`, and `h8_1914`; no current `h8_1475` output path exists. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:189` | `ApplySurfaceCrestRecoveryProbe` begins editor-state mutation before capture. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:203` | Assigns a temp Crest material and serialized `OceanRenderer` fields. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:216` | Calls `ApplyModifiedPropertiesWithoutUndo`. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:227` | Creates temporary `HideAndDontSave` Crest material and writes probe colors/floats. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:249` | Mutates MapMagic graph/settings, pins tile, refreshes, and starts generation. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:315` | Creates temp horizon haze material, activates object, moves/scales it, assigns `sharedMaterial`. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:413` | `QuarantineSurfaceRejectsAndExit` begins destructive quarantine utility. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:442` | Disables renderers. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:457` | Marks scene dirty and saves scene. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:488` | Uses raw `Camera.Render`, `ReadPixels`, `EncodeToPNG`; not a proof packet manifest path. |
| `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs:531` | Writes text metadata, not a ProofGate manifest. |
| `Tools/ProofGate/validate_proof_packet.py:46` | ProofGate requires canonical view set, not arbitrary MCP images. |
| `Tools/ProofGate/validate_proof_packet.py:55` | ProofGate requires manifest fields and strict screenshot metadata. |
| `Tools/ProofGate/validate_proof_packet.py:456` | ProofGate rejects diagnostic substitution. |

Current source no longer references the old deleted `H8_SurfaceWaterReadability_1428.shader` path. Current diagnostic temp haze path is `Assets/_Project/Art/Shaders/H8_SurfaceHorizonHaze_1428.shader`. Stale references to the deleted water-readability probe remain historical rejection context only when explicitly tied to old `h8_1914_surface_water_recovery_probe` artifacts.

## Rejection-Only Methods

- `CaptureSurfaceAndExit`: raw diagnostic screenshot/text only.
- `CaptureSurfacePatchAAndExit`: raw diagnostic screenshot/text only.
- `CaptureWithPoseAndExit` and underwater patch wrappers: rejection-only because they move the camera in memory and do not prove player route ownership.
- `CaptureSurfaceCrestRecoveryProbeAndExit`: rejection-only at best; it applies temp Crest, terrain, and haze mutations before capture.
- `QuarantineSurfaceRejectsAndExit`: not proof tooling. It disables renderers and saves the scene.

## Bacon Recheck - Current 1912 Must Not Be Extended

Bacon's static recheck confirms the current 1912 runner is too dirty for canonical proof:

- it writes raw MCP output under `Docs/Screenshots/MCP`, not `Docs/Screenshots/HectonProofPackets/h8_1475_*`;
- public capture names remain `h8_1912`, `h8_1913`, and `h8_1914`;
- `CaptureSurfaceCrestRecoveryProbeAndExit` mutates scene/render state before capture;
- it disables scene renderers, creates temp materials, writes `OceanRenderer` serialized fields, mutates MapMagic, activates/moves/scales haze objects, and uses `ApplyModifiedPropertiesWithoutUndo`;
- `QuarantineSurfaceRejectsAndExit` is destructive utility, not evidence tooling;
- raw `Camera.Render`/`ReadPixels`/`EncodeToPNG` plus ad hoc text metadata do not satisfy the manifest-bound ProofGate contract.

Result: a future owner must create a separate editor-only harness under `Assets/_Project/Scripts/Editor/Proof/`. Extending 1912 for h8_1475 is rejected.

## Canonical h8_1475 Blockers

- No `h8_1475` output path.
- Raw MCP output, not proof-packet output.
- No `manifest.json`, no `manifest.sha256`, no copied Unity log, no canonical six production screenshots.
- Editor-only visual probes mutate Crest, terrain generation, haze, GameObject activation, transforms, and materials.
- Scene quarantine path can alter `02_HECTON_WORLD.unity`.
- Diagnostic predicate logic is name/token-based, not route-owner-state based.
- Diagnostic images can reject failures but cannot substitute production views.

## Required Repair Route

1. Do not extend `H8VisualProofCapture1912` for canonical acceptance.
2. Create a new no-mutation harness under `Assets/_Project/Scripts/Editor/Proof/`.
3. Output only to `Docs/Screenshots/HectonProofPackets/h8_1475_{session}/`.
4. Hard-ban `SaveScene`, `MarkSceneDirty`, `ApplyModifiedPropertiesWithoutUndo`, renderer disabling, temp material assignment, MapMagic generation, hidden haze probes, and any scene state mutation.
5. Use route-owned production cameras/anchors and read-only serialized readback. No editor cheat camera for canonical proof.
6. Read Crest, terrain, sky, HUD, player, and route predicates without mutating them. Write readback JSON into the packet.
7. Capture the six ProofGate views, compute hashes/sizes/dimensions, write manifest and `manifest.sha256`.
8. Copy Unity log and include offsets plus at least 60 clean post-capture seconds.
9. Run `Tools/ProofGate/validate_proof_packet.py` in strict mode after a clean process gate.

## ProofGate Contract

Source: `Tools/ProofGate/validate_proof_packet.py` and `Tools/ProofGate/README.md`.

Required packet root:

- `Docs/Screenshots/HectonProofPackets/{packet_id}_{session_id}/`

Required files:

- `manifest.json`
- `manifest.sha256`
- `UnityEditor_{packet_id}_{session_id}.log`
- `screenshots/01_surface_coast_aegir_ui_off.png`
- `screenshots/02_shoreline_close_1m.png`
- `screenshots/03_underwater_0_5m.png`
- `screenshots/04_underwater_20_50m_route.png`
- `screenshots/05_aegir_celestial_long.png`
- `screenshots/06_regression_low_oblique.png`

Required manifest fields:

- `schema_name`, `schema_version`, `harness_name`, `harness_version`
- `packet_id`, `session_id`, `created_utc`, `created_local`
- `active_scene`, `evidence_class`, `final_disposition`, `may_submit_as_runtime_proof`
- `global_quality_weight`, `global_quality_label`
- `route_owner_name`, `route_session_id`, `camera_source`, `ui_policy`
- `log_path`, `post_capture_clean_seconds`, `screenshots`

Required derived checks:

- `all_required_views_present`
- `all_required_views_unique`
- `all_required_views_have_sha256`
- `all_production_views_ui_policy_pass`
- `all_depth_predicates_pass`
- `all_route_predicates_pass`
- `quality_weight_is_continuous_float`
- `post_capture_log_window_clean`
- `manifest_written_after_final_screenshot`
- `log_last_write_after_final_screenshot`
- `screenshots_outside_assets_folder`
- `no_asset_import_dependency`

Required screenshot fields:

- `view_index`, `view_id`, `production_view`, `diagnostic_view`
- `file_path`, `file_name`, `sha256`, `byte_size`, `png_width`, `png_height`
- `capture_requested_utc`, `file_created_utc`, `file_last_write_utc`
- `capture_source`, `camera_name`, `camera_position_world`, `camera_rotation_euler`, `field_of_view_degrees`
- `route_anchor_id`, `route_state_id`, `route_state_hash`, `route_predicate_pass`, `route_predicate_failures`
- `camera_visual_depth_meters`, `depth_zone_id`, `depth_zone_name`, `depth_zone_hash`, `depth_predicate_pass`, `underwater_active`
- `global_quality_weight`, `global_quality_label`, `render_scale_current`, `render_scale_target`, `post_stack_hash`, `ui_visible`
- `log_offset_or_timestamp_at_capture`

Hard validator rules:

- `global_quality_weight` must be a continuous float in `[0.0, 1.0]`.
- `global_quality_label` must be `qNNN`; binary labels `low`, `medium`, `high`, and `ultra` are rejected.
- All six production screenshots must exist, match exact filenames, be PNG, be at least `1280x720`, live inside the packet root, and not be under `Assets`.
- `.png.meta` siblings reject the packet.
- Diagnostic screenshots cannot substitute for required production views.
- Manifest `created_utc` and file mtime must be after the final screenshot.
- `manifest.sha256` must match `manifest.json`.
- The Unity log must exist outside `Assets`, be newer than the final screenshot, and provide either clean timestamps or clean offset window for at least 60 seconds.
- Dirty log tokens include `Error`, `Exception`, `Warning`, compile/import/domain reload/ILPP markers, asset refresh markers, leak markers, MCP transport failures, and forced/ready-lock markers.
- Static ProofGate output always has `mayClaimPlayerCaptureVerified: false`; any manifest or derived check trying to claim player-capture verification is rejected.

View-specific predicates:

- `surface_coast_aegir_ui_off`: `ui_visible` must be false.
- `shoreline_close_1m`: non-empty `route_anchor_id`.
- `underwater_0_5m`: depth in `0.25-5.0` meters and `underwater_active: true`.
- `underwater_20_50m_route`: depth in `20.0-50.0` meters and `underwater_active: true`.
- `aegir_celestial_long`: non-empty `route_anchor_id`.
- `regression_low_oblique`: non-empty `route_anchor_id`.

Strict validator command:

```powershell
python Tools\ProofGate\validate_proof_packet.py `
  --packet-root Docs\Screenshots\HectonProofPackets\h8_1475_s01 `
  --packet-id h8_1475 `
  --session-id s01 `
  --expected-quality q060 `
  --json-out Docs\Reports\Batch28\ProofPacketGate_h8_1475_s01.json `
  --md-out Docs\Reports\Batch28\ProofPacketGate_h8_1475_s01.md `
  --strict
```

## Low / Middle / High / Ultra Consequences

- Low: proof still uses the same route predicates and production state; lower visual density is allowed only if the surface/shallow floor remains beautiful and readable.
- Middle: same truth route with normal production density and clean UI policy.
- High: longer sightline and richer material/lighting proof, no changed route truth.
- Ultra: extra polish captures may exist, but canonical acceptance remains the same six-view no-mutation packet plus route/HUD/player proof.

Final status: `PENDING VERIFICATION / PROOF_TOOL_RISK`.
