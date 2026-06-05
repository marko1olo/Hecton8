# HECTON ProofGate

Static validator for HECTON visual proof packets.

Evidence class: `STATIC_FILESYSTEM` / `STATIC_MANIFEST` / `STATIC_LOG`.

This tool does not launch Unity, enter Play Mode, profile, judge visual quality, or verify player capture truth. A pass means the packet may proceed to runtime/human visual review. It is not acceptance.

## Required Packet Root

```text
Docs/Screenshots/HectonProofPackets/{packet_id}_{session_id}/
```

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

Diagnostic screenshots may exist only when the manifest marks them diagnostic. They cannot substitute for production views.

## Command

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

Exit codes:

- `0`: `PASS_STATIC_GATE`
- `1`: `REJECTED_STATIC_GATE`
- `2`: malformed CLI/path/schema input
- `3`: internal validator failure

## Rejection Examples

The gate rejects:

- raw PNG folders without `manifest.json`;
- missing production views;
- diagnostic substitution;
- mismatched SHA256, byte size, PNG dimensions, or timestamps;
- screenshots or packet roots under any `Assets` path;
- `.png.meta` siblings in proof screenshots;
- binary quality labels like `low`, `medium`, `high`, `ultra`;
- invalid underwater depth bands;
- false route/depth predicates;
- stale, short, missing, or dirty logs;
- dirty tokens inside declared log-window offsets.

## Log Window

Preferred manifest fields:

```json
{
  "log_window_start_utc": "2026-06-04T18:00:00Z",
  "log_window_end_utc": "2026-06-04T18:01:01Z",
  "log_window_start_offset": 102400,
  "log_window_end_offset": 109800,
  "post_capture_clean_seconds": 61
}
```

If offsets are present, the validator scans only that log slice for dirty tokens. If offsets are absent, strict review should treat the full-file scan warning as residual risk.

## Tests

```powershell
python -m unittest discover -s Tools\ProofGate -p test_*.py
```

Current expected result:

```text
Ran 17 tests
OK
```
