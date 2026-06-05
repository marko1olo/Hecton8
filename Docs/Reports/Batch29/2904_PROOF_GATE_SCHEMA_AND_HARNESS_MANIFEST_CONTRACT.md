# 2904 Proof Gate Schema And Harness Manifest Contract

Status: `STATIC VERIFIED`
Agent: `2904_PROOF_GATE_SCHEMA_AND_HARNESS_MANIFEST_CONTRACT`
Date: 2026-06-04
Workspace: `C:\hades\Hecton8`
Evidence class: `STATIC_SOURCE` + `STATIC_DOC` + `STATIC_FILESYSTEM` + `STATIC_CLI`

## Boundary

No Unity Editor run, Play Mode run, build, source edit, Python edit, asset edit, process kill, profiler capture, screenshot capture, or visual acceptance was performed.

Owned write path used:

- `Docs/Reports/Batch29/2904_PROOF_GATE_SCHEMA_AND_HARNESS_MANIFEST_CONTRACT.md`

## Authority And Evidence Read

| Evidence label | Artifact | Result |
|---|---|---|
| `STATIC_DOC` | `AGENTS.md` | Static proof cannot become runtime proof. Continuous `GlobalQualityWeight` is mandatory. |
| `STATIC_DOC` | `quality.md` | Valid proof labels and proof matrix loaded. Static schemas remain `PENDING VERIFICATION` for runtime claims. |
| `STATIC_DOC` | `testing.md` | Evidence classes loaded. Static scans are valid for schema/file/token checks only. |
| `STATIC_DOC` | `.agents-skills/QA_Evidence_Text_Filter_Audit.txt` | Evidence label anti-lie rules loaded. |
| `STATIC_DOC` | `Docs/Reports/Batch28/2805_LOG_PROCESS_PROOF_GATE_TOOL_AUDIT.md` | Original intended ProofGate schema and reject model loaded. |
| `STATIC_DOC` | `Docs/Reports/Batch28/BATCH28_SYNTHESIS_FOR_UNITY_OWNER.md` | Current Batch28 synthesis loaded. Harness still does not exist. Validator exists. |
| `STATIC_SOURCE` | `Tools/ProofGate/validate_proof_packet.py` | Current validator contract inspected. |
| `STATIC_SOURCE` | `Tools/ProofGate/test_validate_proof_packet.py` | 17-test suite inspected. |
| `STATIC_CLI` | `python -m unittest discover -s Tools/ProofGate -p test_*.py` | `Ran 17 tests in 1.225s` / `OK`. CLI test prints `PASS_STATIC_GATE`; this is static gate output, not runtime proof. |
| `STATIC_CLI` | `python Tools/ProofGate/validate_proof_packet.py --packet-root Docs/Screenshots/MCP --packet-id h8_1474 --session-id mcp --strict` | Exit code 1. Output: `REJECTED_STATIC_GATE`, `RAW_PNG_SET`. |

`Docs/Actual Domains of Project.txt` exists check returned no content. Narrow domain: proof packet schema, static validation, screenshot/log evidence gate.

## Current Verdict

`Tools/ProofGate` defines a static packet validator, not a Unity harness. A future Unity C# harness must emit a manifest that satisfies the Python contract exactly or it will be rejected before human visual review.

Evidence status:

- Schema contract: `STATIC VERIFIED`.
- Validator unit tests: `STATIC_CLI`.
- Unity harness existence: `PENDING VERIFICATION` / blocked; Batch28 synthesis states no first-party harness exists.
- Runtime capture truth: `PENDING VERIFICATION`.
- Visual quality: `PENDING VERIFICATION`.
- Profiler, GC, Frame Debugger, player build, device proof: `PENDING VERIFICATION`.

## Exact Manifest Schema Table

Top-level manifest file: `manifest.json`
Schema name required by validator output: `hecton8.proof_packet_gate.v1`

| Field | Required | Type / Format | Validator rule | Harness obligation |
|---|---:|---|---|---|
| `schema_name` | yes | string | Present only; no exact equality check in current validator. | Emit `hecton8.proof_packet_gate.v1`. |
| `schema_version` | yes | number or string accepted by current validator | Present only. | Emit stable integer, recommended `1`. |
| `harness_name` | yes | non-empty string expected; current validator checks presence only | Present only. | Emit owned harness name. |
| `harness_version` | yes | string | Present only. | Emit deterministic harness version. |
| `packet_id` | yes | string | Must equal CLI `--packet-id`. | Match packet folder/CLI exactly. |
| `session_id` | yes | string | Must equal CLI `--session-id`. | Match packet folder/CLI exactly. |
| `created_utc` | yes | ISO-8601 timestamp | Must parse; must be later than final screenshot timestamp. | Write after final screenshot. |
| `created_local` | yes | timestamp/string | Present only. | Emit local time for operator trace. |
| `active_scene` | yes | string | Present only. | Emit actual active scene, expected `02_HECTON_WORLD` for route proof. |
| `evidence_class` | yes | string | Present only. | Must not claim profiler/device/human visual acceptance. |
| `final_disposition` | yes | string | If `ACCEPTED_BY_HARNESS`, `may_submit_as_runtime_proof` must be true. | Use only after harness-owned checks pass. |
| `may_submit_as_runtime_proof` | yes | boolean | Conflict reject when disposition accepted but flag not true. | Does not grant visual/profiler/player-capture proof. |
| `global_quality_weight` | yes | number `0.0..1.0` | Reject if non-numeric or outside range. | Continuous scalar; no binary tier. |
| `global_quality_label` | yes | `qNNN` string | Rejects `low`, `medium`, `high`, `ultra`; rejects non-`q\d{3}`. | Derive from continuous weight, e.g. `q060`. |
| `route_owner_name` | yes | string | Present only. | Emit owner that proved route predicates. |
| `route_session_id` | yes | string | Present only. | Emit route capture session id. |
| `camera_source` | yes | string | Present only. | Emit owned harness/camera source. |
| `ui_policy` | yes | string | Present only. | Must align with screenshot records, especially UI-off view. |
| `log_path` | yes | path string | Must resolve to existing log; must not be under `Assets`. | Prefer packet-local copied editor log. |
| `log_sha256` | no | 64 hex string | If present/non-empty, must match log SHA256. | Emit it to prevent log swap. |
| `log_window_start_utc` | no | ISO-8601 timestamp | If paired with end, duration must meet minimum. | Useful for human trace; offset window is stronger. |
| `log_window_end_utc` | no | ISO-8601 timestamp | If paired with start, duration must meet minimum. | Must close after final screenshot. |
| `log_window_start_offset` | no | integer byte/character offset in Python string text | If paired with end, validator scans only this slice. Invalid range rejects. | Emit exact clean-window start offset. |
| `log_window_end_offset` | no | integer byte/character offset in Python string text | Must be `>= start` and `<= len(text)`. Invalid range rejects. | Emit exact clean-window end offset. |
| `post_capture_clean_seconds` | yes | number | Must be at least CLI `--min-post-capture-clean-seconds`, default 60. | Minimum clean closure proof, not a substitute for offsets. |
| `derived_checks` | yes by behavior | object | Missing/non-object rejects; every required derived check must be true. | Emit all checks below as true only from harness-owned facts. |
| `screenshots` | yes | array of screenshot records | Must be list. Six production views must exist exactly once by `view_id`. | Emit six required production records. |
| `allow_mixed_dimensions` | optional future field | boolean | Mentioned in Batch28 audit but not enforced by current validator. | Do not rely on it until validator implements it. |

Required `derived_checks` object keys:

| Key | Required value | Meaning |
|---|---:|---|
| `all_required_views_present` | `true` | All six production views are in `screenshots`. |
| `all_required_views_unique` | `true` | No duplicate production view identity. |
| `all_required_views_have_sha256` | `true` | Screenshot records carry hashes. |
| `all_production_views_ui_policy_pass` | `true` | UI state predicates passed. |
| `all_depth_predicates_pass` | `true` | Depth checks passed for required depth views. |
| `all_route_predicates_pass` | `true` | Route owner accepted all required route predicates. |
| `quality_weight_is_continuous_float` | `true` | `GlobalQualityWeight` is scalar, not binary tier. |
| `post_capture_log_window_clean` | `true` | Declared clean log window contains no forbidden tokens. |
| `manifest_written_after_final_screenshot` | `true` | Manifest created after final screenshot. |
| `log_last_write_after_final_screenshot` | `true` | Log closure is after final screenshot. |
| `screenshots_outside_assets_folder` | `true` | No screenshots under `Assets`. |
| `no_asset_import_dependency` | `true` | Capture did not depend on Unity importing screenshot files. |

## Required Screenshot Record Schema Table

Each required production record must appear in `manifest.screenshots`.

Required production views:

| Index | `view_id` | Required `file_name` |
|---:|---|---|
| 1 | `surface_coast_aegir_ui_off` | `01_surface_coast_aegir_ui_off.png` |
| 2 | `shoreline_close_1m` | `02_shoreline_close_1m.png` |
| 3 | `underwater_0_5m` | `03_underwater_0_5m.png` |
| 4 | `underwater_20_50m_route` | `04_underwater_20_50m_route.png` |
| 5 | `aegir_celestial_long` | `05_aegir_celestial_long.png` |
| 6 | `regression_low_oblique` | `06_regression_low_oblique.png` |

Per-record fields:

| Field | Required | Type / Format | Validator rule | Harness obligation |
|---|---:|---|---|---|
| `view_index` | yes | integer | Must match required index for six production views. | Emit 1-6 exactly. |
| `view_id` | yes | string | Must match one required view id. Duplicate ids reject. | Use canonical ids only. |
| `production_view` | yes | boolean | Must be `true` for each required view. | Diagnostic cannot replace production. |
| `diagnostic_view` | yes | boolean | Must not be `true` for required views. | Keep diagnostics separate. |
| `file_path` | yes | path string | Must exist; must resolve inside packet root; must not be under `Assets`; extension `.png`. | Prefer `screenshots/<file_name>`. |
| `file_name` | yes | string | Must match actual file name and required file name. | Filename and view id must agree. |
| `sha256` | yes | 64 hex string | Must match file SHA256. | Hash after file write closes. |
| `byte_size` | yes | integer | Must equal filesystem size. | Record exact size. |
| `png_width` | yes | integer | Must match PNG IHDR; production must be at least 1280. | Do not fake dimensions. |
| `png_height` | yes | integer | Must match PNG IHDR; production must be at least 720. | Do not fake dimensions. |
| `capture_requested_utc` | yes | ISO-8601 timestamp | Presence only in current validator. | Emit request time. |
| `file_created_utc` | yes | ISO-8601 timestamp | Presence only in current validator. | Emit creation time. |
| `file_last_write_utc` | yes | ISO-8601 timestamp | Must parse and match filesystem mtime within 5 seconds. | Set after write and before manifest. |
| `capture_source` | yes | string | Presence only. | Emit actual capture path/source. |
| `camera_name` | yes | string | Presence only. | Emit exact camera object/name. |
| `camera_position_world` | yes | array/object | Presence only. | Use deterministic numeric coordinates. |
| `camera_rotation_euler` | yes | array/object | Presence only. | Use deterministic numeric rotation. |
| `field_of_view_degrees` | yes | number | Presence only. | Emit actual FOV. |
| `route_anchor_id` | yes | string | Required non-empty for shoreline, Aegir, and regression views. | Emit owner-proven route anchor. |
| `route_state_id` | yes | string | Presence only. | Emit route state id. |
| `route_state_hash` | yes | string | Presence only. | Emit deterministic route state hash. |
| `route_predicate_pass` | yes | boolean | Must be `true` for all required views. | Do not infer from filename. |
| `route_predicate_failures` | yes | array | Presence only. | Empty only when predicate truly passed. |
| `camera_visual_depth_meters` | yes | number | Enforced for underwater views. | Use owner-derived camera depth. |
| `depth_zone_id` | yes | string | Presence only. | Emit actual depth zone id. |
| `depth_zone_name` | yes | string | Presence only. | Emit actual depth zone name. |
| `depth_zone_hash` | yes | string | Presence only. | Emit deterministic depth zone hash. |
| `depth_predicate_pass` | yes | boolean | Must be `true` for all required views in current validator. | Surface views still need a true predicate record. |
| `underwater_active` | yes | boolean | Must be `true` for both underwater views. | Query underwater owner for exact capture camera. |
| `global_quality_weight` | yes | number `0.0..1.0` | Reject if non-numeric or out of range. | Match manifest weight. |
| `global_quality_label` | yes | `qNNN` string | Reject binary labels and non-`qNNN`; must match `--expected-quality` if supplied. | Match manifest label. |
| `render_scale_current` | yes | number | Presence only. | Emit actual current render scale. |
| `render_scale_target` | yes | number | Presence only. | Emit intended target render scale. |
| `post_stack_hash` | yes | string | Presence only. | Hash material/post/toggle state. |
| `ui_visible` | yes | boolean | Must be `false` for `surface_coast_aegir_ui_off`. | Emit actual UI visibility. |
| `log_offset_or_timestamp_at_capture` | yes | integer or timestamp string | Presence only. | Prefer exact log offset for capture moment. |
| `packet_id` | optional by field list, enforced if present | string | If present, must match CLI packet id. | Include it for serializer clarity. |
| `session_id` | optional by field list, enforced if present | string | If present, must match CLI session id. | Include it for serializer clarity. |

Static PNG gates:

- PNG magic bytes must be valid: `89 50 4E 47 0D 0A 1A 0A`.
- IHDR dimensions must match manifest dimensions.
- File size and SHA256 must match.
- Production dimensions below `1280x720` reject.
- `.png.meta` sibling rejects.
- Path outside packet root rejects.
- Path under `Assets` rejects.

View-specific gates:

| View | Extra static predicate |
|---|---|
| `surface_coast_aegir_ui_off` | `ui_visible` must be `false`. |
| `shoreline_close_1m` | `route_anchor_id` must be non-empty. Current validator does not enforce route distance field despite Batch28 audit request. |
| `underwater_0_5m` | `camera_visual_depth_meters >= 0.25` and `<= 5.0`; `underwater_active == true`. |
| `underwater_20_50m_route` | `camera_visual_depth_meters >= 20.0` and `<= 50.0`; `underwater_active == true`. |
| `aegir_celestial_long` | `route_anchor_id` must be non-empty. Current validator does not enforce celestial-specific proof fields. |
| `regression_low_oblique` | `route_anchor_id` must be non-empty. Current validator does not enforce oblique camera predicate proof beyond route predicate/pass fields. |

## Rejected Status / Label List

Validator output statuses:

| Status | Meaning | Evidence limit |
|---|---|---|
| `PASS_STATIC_GATE` | Packet schema/files/logs passed static validator. | May submit for human visual review. Does not mean visual/runtime/profiler accepted. |
| `REJECTED_STATIC_GATE` | Static validator rejected packet. | Human visual review blocked. |
| `PENDING_STATIC_GATE` | Defined constant but not currently emitted by normal code path inspected. | Do not rely on this status until implemented. |

Forbidden or rejected labels/claims:

| Label / claim | Contract result |
|---|---|
| `PLAYER-CAPTURE VERIFIED` | Forbidden for static gate. Requires runtime/human evidence beyond validator. |
| `PROFILER VERIFIED` | Forbidden for static gate. Requires profiler artifact. |
| `PLAYMODE VERIFIED` / `PLAYMODE_TESTED` | Forbidden for static gate. Requires Play Mode run. |
| `VISUAL ACCEPTED` | Forbidden for static gate. Requires human visual gate and screenshot review. |
| `RELEASE READY` | Forbidden. Requires release proof matrix. |
| `low`, `medium`, `high`, `ultra` as `global_quality_label` | Rejected as binary quality labels. |
| Any non-`qNNN` `global_quality_label` | Rejected by regex. |
| Raw screenshot folder without manifest | `REJECTED_STATIC_GATE` / `RAW_PNG_SET`. |

Current reject codes emitted by validator source:

| Reject code | Trigger |
|---|---|
| `RAW_PNG_SET` | PNGs exist but `manifest.json` is missing. |
| `MISSING_MANIFEST` | No manifest and no PNG set. |
| `MANIFEST_MALFORMED` | Manifest cannot be decoded as object JSON. |
| `MANIFEST_SHA_MISSING` | `manifest.sha256` missing. |
| `MANIFEST_SHA_MALFORMED` | SHA file lacks 64-hex digest. |
| `MANIFEST_SHA_MISMATCH` | Manifest digest mismatch. |
| `MANIFEST_FIELD_MISSING` | Required top-level fields absent. |
| `PACKET_ID_MISMATCH` | Manifest packet id differs from CLI. |
| `SESSION_ID_MISMATCH` | Manifest session id differs from CLI. |
| `QUALITY_WEIGHT_INVALID` | Weight missing, non-numeric, or outside `0.0..1.0`. |
| `BINARY_QUALITY_LABEL` | Binary or non-`qNNN` label. |
| `QUALITY_LABEL_MISMATCH` | Label differs from CLI `--expected-quality`. |
| `MANIFEST_ACCEPTANCE_CONFLICT` | Accepted disposition without runtime-proof flag. |
| `DERIVED_CHECKS_MISSING` | `derived_checks` absent or not object. |
| `DERIVED_CHECK_FALSE` | Required derived check missing or false. |
| `LOG_WINDOW_TOO_SHORT` | Post-capture or timestamp window below minimum. |
| `SCREENSHOTS_FIELD_INVALID` | `screenshots` is not a list. |
| `ASSET_IMPORT_DEPENDENCY` | `no_asset_import_dependency` missing or false. |
| `SCREENSHOT_RECORD_INVALID` | Screenshot record is not an object. |
| `SCREENSHOT_FIELD_MISSING` | Required screenshot fields absent. |
| `DUPLICATE_VIEW` | Duplicate `view_id`. |
| `SCREENSHOT_MISSING` | File path missing. |
| `SCREENSHOT_OUTSIDE_PACKET` | File path resolves outside packet root. |
| `SCREENSHOT_UNDER_ASSETS` | Screenshot path under `Assets`. |
| `SCREENSHOT_NOT_PNG` | Extension is not `.png`. |
| `SCREENSHOT_META_SIBLING` | Unity `.meta` sibling exists beside screenshot. |
| `SCREENSHOT_FILENAME_MISMATCH` | Actual file name differs from record `file_name`. |
| `PNG_INVALID` | PNG magic/IHDR invalid. |
| `PNG_DIMENSION_MISMATCH` | IHDR dimensions differ from manifest. |
| `PNG_DIMENSION_TOO_SMALL` | Production PNG below `1280x720`. |
| `PNG_BYTE_SIZE_MISMATCH` | Manifest byte size differs from filesystem. |
| `PNG_SHA256_MISMATCH` | Manifest SHA256 differs from file. |
| `SCREENSHOT_TIMESTAMP_INVALID` | `file_last_write_utc` cannot parse. |
| `SCREENSHOT_TIMESTAMP_MISMATCH` | Filesystem mtime differs by more than 5 seconds. |
| `SCREENSHOT_PACKET_ID_MISMATCH` | Optional record packet id differs from CLI. |
| `SCREENSHOT_SESSION_ID_MISMATCH` | Optional record session id differs from CLI. |
| `REQUIRED_VIEW_MISSING` | One required view id absent. |
| `VIEW_INDEX_MISMATCH` | Required view has wrong index. |
| `FALSE_ROUTE_LABEL` | Required file/view/anchor predicate mismatch. |
| `PRODUCTION_VIEW_MISSING` | Required view is not production. |
| `DIAGNOSTIC_SUBSTITUTION` | Required production view marked diagnostic. |
| `ROUTE_PREDICATE_FAIL` | Route predicate not true. |
| `DEPTH_PREDICATE_FAIL` | Depth predicate not true or depth outside band. |
| `UNDERWATER_INACTIVE` | Underwater view not marked active. |
| `UI_POLICY_FAIL` | UI-off surface view has UI visible. |
| `UNKNOWN_SCREENSHOT_FILE` | Strict mode finds unrecognized screenshot PNG. |
| `MANIFEST_TIMESTAMP_INVALID` | `created_utc` cannot parse. |
| `MANIFEST_STALE` | Manifest created/modified before final screenshot. |
| `MISSING_LOG` | Declared log path missing. |
| `LOG_UNDER_ASSETS` | Log path under `Assets`. |
| `STALE_LOG` | Log mtime predates final screenshot. |
| `LOG_SHA256_MISMATCH` | Log SHA256 mismatch. |
| `LOG_WINDOW_MISSING` | No timestamp window and no numeric clean seconds. |
| `LOG_WINDOW_OFFSET_INVALID` | Declared offsets invalid for log text length. |
| `PACKET_UNDER_ASSETS` | Packet root under `Assets`. |
| `DIRTY_LOG_ERROR` | Clean window contains error-class forbidden token. |
| `DIRTY_LOG_WARNING` | Clean window contains warning-class forbidden token. |
| `DIRTY_LOG_LEAK` | Clean window contains leak token. |
| `DIRTY_LOG_IMPORT` | Clean window contains import/AssetDatabase/package token. |
| `DIRTY_LOG_COMPILE` | Clean window contains compile token. |
| `DIRTY_LOG_DOMAIN_RELOAD` | Clean window contains domain reload token. |
| `DIRTY_LOG_ILPP` | Clean window contains ILPP token. |
| `DIRTY_LOG_MCP_TRANSPORT` | Clean window contains MCP transport token. |

## Clean Log Offset Contract

The clean log contract must be offset-first.

Required behavior:

1. Harness copies or declares a stable log path outside `Assets`.
2. Harness records a clean-window start and end after capture closure.
3. Harness emits `log_window_start_offset` and `log_window_end_offset` when possible.
4. Validator scans only `log[start:end]` when both offsets are valid integers.
5. Offset range must satisfy: `start >= 0`, `end >= start`, `end <= len(log_text)`.
6. If offsets are missing, current validator scans the full file and appends warning `LOG_WINDOW_SCAN_FULL_FILE: no log offsets; scanned full log.`
7. Timestamp windows are accepted only for duration math; current validator does not slice log text by timestamps.
8. `post_capture_clean_seconds` must be at least the CLI threshold, default 60.
9. Log mtime must be after final screenshot timestamp.
10. Optional `log_sha256`, when present, must match the copied log.

Forbidden tokens in the scanned clean window:

| Reject code | Tokens |
|---|---|
| `DIRTY_LOG_ERROR` | `Error`, `Exception`, `LogError`, `shader error`, `material error`, `H8_PLAYMODE_EXIT`, `Access token is unavailable`, `ready lock` |
| `DIRTY_LOG_WARNING` | `Warning`, `forced` |
| `DIRTY_LOG_LEAK` | `Found 1 leak`, `Leak Detected` |
| `DIRTY_LOG_IMPORT` | `not valid. Loading of assembly skipped`, `Asset Pipeline Refresh`, `Library/PackageCache`, `AssetDatabase.Refresh`, `RefreshInfo` |
| `DIRTY_LOG_COMPILE` | `CompileScripts` |
| `DIRTY_LOG_DOMAIN_RELOAD` | `Domain Reload`, `ReloadAssembly` |
| `DIRTY_LOG_ILPP` | `ILPP`, `PostProcessing ILPP` |
| `DIRTY_LOG_MCP_TRANSPORT` | `MCP WebSocket connection failed`, `failed to start MCP transport` |

Harness note: if using C# byte offsets into UTF-8 files, make the Python validator contract explicit before implementation. Current Python slicing uses decoded string length, not raw byte indexing.

## Sample Minimal Accepted Manifest Excerpt

This excerpt is structural only. Hashes, byte sizes, mtimes, and dimensions must be generated from real files or the validator rejects it.

```json
{
  "schema_name": "hecton8.proof_packet_gate.v1",
  "schema_version": 1,
  "harness_name": "HectonProofHarness",
  "harness_version": "1.0",
  "packet_id": "h8_1475",
  "session_id": "s01",
  "created_utc": "2026-06-04T18:02:30Z",
  "created_local": "2026-06-04T22:02:30+04:00",
  "active_scene": "02_HECTON_WORLD",
  "evidence_class": "UNITY_CAPTURE_PACKET",
  "final_disposition": "ACCEPTED_BY_HARNESS",
  "may_submit_as_runtime_proof": true,
  "global_quality_weight": 0.6,
  "global_quality_label": "q060",
  "route_owner_name": "HectonProofHarness",
  "route_session_id": "s01",
  "camera_source": "owned_harness",
  "ui_policy": "ui_off",
  "log_path": "UnityEditor_h8_1475_s01.log",
  "log_sha256": "<64_hex_log_sha256>",
  "log_window_start_offset": 12840,
  "log_window_end_offset": 14320,
  "log_window_start_utc": "2026-06-04T18:01:00Z",
  "log_window_end_utc": "2026-06-04T18:02:01Z",
  "post_capture_clean_seconds": 61,
  "derived_checks": {
    "all_required_views_present": true,
    "all_required_views_unique": true,
    "all_required_views_have_sha256": true,
    "all_production_views_ui_policy_pass": true,
    "all_depth_predicates_pass": true,
    "all_route_predicates_pass": true,
    "quality_weight_is_continuous_float": true,
    "post_capture_log_window_clean": true,
    "manifest_written_after_final_screenshot": true,
    "log_last_write_after_final_screenshot": true,
    "screenshots_outside_assets_folder": true,
    "no_asset_import_dependency": true
  },
  "screenshots": [
    {
      "view_index": 1,
      "view_id": "surface_coast_aegir_ui_off",
      "production_view": true,
      "diagnostic_view": false,
      "file_path": "screenshots/01_surface_coast_aegir_ui_off.png",
      "file_name": "01_surface_coast_aegir_ui_off.png",
      "sha256": "<64_hex_png_sha256>",
      "byte_size": 123456,
      "png_width": 1280,
      "png_height": 720,
      "capture_requested_utc": "2026-06-04T18:00:00Z",
      "file_created_utc": "2026-06-04T18:00:01Z",
      "file_last_write_utc": "2026-06-04T18:00:01Z",
      "capture_source": "owned_harness",
      "camera_name": "ProofCamera_surface_coast_aegir_ui_off",
      "camera_position_world": [0.0, 0.0, 0.0],
      "camera_rotation_euler": [0.0, 0.0, 0.0],
      "field_of_view_degrees": 60.0,
      "route_anchor_id": "anchor_surface_coast_aegir_ui_off",
      "route_state_id": "state_surface_coast_aegir_ui_off",
      "route_state_hash": "0x12345678",
      "route_predicate_pass": true,
      "route_predicate_failures": [],
      "camera_visual_depth_meters": 0.0,
      "depth_zone_id": "surface",
      "depth_zone_name": "surface",
      "depth_zone_hash": "0x87654321",
      "depth_predicate_pass": true,
      "underwater_active": false,
      "global_quality_weight": 0.6,
      "global_quality_label": "q060",
      "render_scale_current": 1.0,
      "render_scale_target": 1.0,
      "post_stack_hash": "0xabcdef01",
      "ui_visible": false,
      "log_offset_or_timestamp_at_capture": "2026-06-04T18:00:01Z",
      "packet_id": "h8_1475",
      "session_id": "s01"
    }
  ]
}
```

Accepted packet must include all six screenshot records, not only the one shown above.

## Compatibility Risks Between Validator And Planned Unity C# Serializer

| Risk | Evidence class | Impact | Required decision before harness |
|---|---|---|---|
| Python log offsets are decoded string indices, not raw byte offsets. | `STATIC_SOURCE` | C# byte offsets can reject valid clean windows or scan wrong text slice. | Define offsets as UTF-8 byte offsets and patch validator, or make C# compute Python-compatible UTF-16/decoded string indices. Byte offsets are cleaner. |
| `schema_name` and `schema_version` are presence-only. | `STATIC_SOURCE` | Harness could emit wrong schema and still pass. | Add exact schema/version tests before serializer freeze. |
| `evidence_class` is presence-only. | `STATIC_SOURCE` | Harness could emit inflated evidence class and still pass static gate. | Add allowed static/capture packet label list; reject profiler/player/device labels. |
| `final_disposition: ACCEPTED_BY_HARNESS` with `may_submit_as_runtime_proof: true` can pass static gate. | `STATIC_SOURCE` | Wording can be misread as runtime/visual acceptance. | Harness report text must state static gate pass only; final human visual status remains separate. |
| `allow_diagnostic_view` CLI flag exists but is unused. | `STATIC_SOURCE` | Future harness may assume optional diagnostics are explicitly permitted. | Either implement diagnostic handling or remove the flag. |
| Current validator requires `depth_predicate_pass == true` for surface views too. | `STATIC_SOURCE` | C# serializer must emit true surface depth predicates even if semantically odd. | Keep for v1 compatibility; clarify as "depth predicate not applicable but pass". |
| Batch28 audit requested shoreline distance, celestial proof, oblique proof, and mixed dimension policy; current validator does not enforce all of them. | `STATIC_DOC` + `STATIC_SOURCE` | A manifest may pass without enough route-specific proof detail. | Add fields/tests before accepting harness output as stable. |
| Timestamp parse accepts naive timestamps as UTC. | `STATIC_SOURCE` | C# local timestamps without timezone can be misclassified. | Emit timezone-qualified UTC strings with `Z`. |
| `file_created_utc` is presence-only. | `STATIC_SOURCE` | Creation time can drift without rejection. | Use `file_last_write_utc` as authority or add creation validation. |
| `camera_position_world`, rotations, and hashes are presence-only. | `STATIC_SOURCE` | Serializer can emit wrong shapes/types and still pass. | Add type/range checks for numeric arrays and hash patterns. |
| JSON naming is snake_case. | `STATIC_SOURCE` | Unity default serializers often use PascalCase fields or omit unsupported structures. | Use explicit DTO with fixed property names or a deterministic JSON writer. |
| SHA files accept any text containing a 64-hex digest. | `STATIC_SOURCE` | Loose `manifest.sha256` format can hide operator mistakes. | Require exact `<sha>  manifest.json` if the harness owns output. |

## Proposed Extra Tests

These are proposed only. No test files were edited.

| Test | Evidence need | Reason |
|---|---|---|
| Reject wrong `schema_name`. | `STATIC_SOURCE` | Current field is presence-only. |
| Reject unsupported `schema_version`. | `STATIC_SOURCE` | Prevent silent v1/v2 drift. |
| Reject inflated manifest `evidence_class`, including `PLAYER-CAPTURE VERIFIED`, `PROFILER VERIFIED`, `DEVICE_VERIFIED`, `VISUAL ACCEPTED`. | `STATIC_SOURCE` | Stops label fraud at manifest gate. |
| Reject `may_submit_as_runtime_proof: true` unless `final_disposition == ACCEPTED_BY_HARNESS` and all derived/static checks pass. | `STATIC_SOURCE` | Current code only checks one conflict direction. |
| Reject malformed `camera_position_world` and `camera_rotation_euler` types. | `STATIC_SOURCE` | C# serializer must not emit strings/objects accidentally. |
| Reject non-empty `route_predicate_failures` when `route_predicate_pass == true`. | `STATIC_SOURCE` | Avoid contradictory predicate records. |
| Require shoreline close view route distance field and enforce `<= 1.0m` or documented tolerance. | `STATIC_SOURCE` | Batch28 audit required shoreline-close proof; current validator only checks anchor id. |
| Require `aegir_celestial_long` celestial owner/proof field. | `STATIC_SOURCE` | Prevent generic sky screenshot passing with only anchor id. |
| Require `regression_low_oblique` oblique camera predicate field. | `STATIC_SOURCE` | Prevent false regression view label. |
| Test `.sha256` exact format. | `STATIC_SOURCE` | Reduces ambiguous manifest hash files. |
| Test byte-offset clean log contract after validator is patched to byte offsets. | `STATIC_SOURCE` | C# compatibility requires exact definition. |
| Test `--allow-diagnostic-view` behavior or remove flag. | `STATIC_SOURCE` | Current flag has no effect. |
| Test mixed dimensions rejection and explicit allowed policy if implemented. | `STATIC_SOURCE` | Batch28 audit mentions it; current validator does not enforce it. |

## First-20-Minutes Route Impact

This work removes a process blocker only: it defines the static manifest contract that a future route recovery harness must satisfy before the first-20-minutes visual packet can be reviewed. It does not improve graphics, optimization, or gameplay by itself.

## Scalability Consequences

- Low: static manifest/log/PNG validation runs without Unity, GPU, import, or build. It does not lower the visual floor.
- Middle: same six route truths and `qNNN` quality label must be emitted; no binary quality path.
- High: harness may add richer diagnostic records, but production schema and route truth remain identical.
- Ultra: additional overkill diagnostics can be appended only after the six production views and clean log contract pass; no schema drift that changes gameplay truth or visual acceptance labels.

## Strongest Blockers

1. No Unity C# proof harness exists yet. Static schema contract is not runtime proof.
2. Current validator does not enforce exact `schema_name`, `schema_version`, allowed `evidence_class`, or all Batch28 route-specific proof fields.
3. Log offset semantics are incompatible by default unless C# and Python agree on decoded-string offsets versus UTF-8 byte offsets.
4. The raw `Docs/Screenshots/MCP` folder remains rejected as `RAW_PNG_SET`; it has no manifest route/depth/quality/log proof.
5. Static gate cannot judge visual quality, Subnautica-level surface/shallow floor, profiler state, GC, player capture truth, or device readiness.

## Final Contract Sentence

The future Unity harness must emit one packet-local `manifest.json`, `manifest.sha256`, copied clean-window log, and six production PNG records whose file identity, hashes, dimensions, timestamps, route predicates, depth predicates, UI state, continuous `GlobalQualityWeight`, `qNNN` label, and clean log offsets agree with the current Python validator. Anything less is `REJECTED_STATIC_GATE`, not a proof packet.
