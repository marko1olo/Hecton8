# Unity Owner Steer 2026-06-04 1466 Green Surface Reject

Target visible thread: `Продолжить работу по логам`

Use as steer only.

## Evidence Reviewed

- `Docs/Screenshots/MCP/h8_1466_surface_clean_ui_off.png`
- `Docs/Screenshots/MCP/h8_1466_shoreline_clean_1m.png`
- `Docs/Screenshots/MCP/h8_1466_regression_low_oblique_clean.png`
- `Docs/AgentLogs/UnityEditor_visual_audit_restart.log`

## Steer Text

1466 is NOT accepted.

Good:
- The obvious surface placeholder fish/silhouette clutter appears removed.

Reject:
- Ocean surface is now over-driven acid green. It reads as a flat tinted plane with black streak artifacts, not premium ocean water.
- This is not the requested bright beautiful surface. Bright does not mean neon green or posterized.
- Shoreline/terrain still reads as a grey/yellow striped terrain shell with weak authored coastal breakup.
- Small brown celestial dot still reads primitive.
- No underwater proof was included in the 1466 clean packet, so 0-5 m and 20-50 m route remain unproven after the earlier underwater reject.
- Previous runtime fault still matters: 1465 log had repeated `ArgumentNullException` plus `H8_PLAYMODE_EXIT_AFTER_INVALID_FORCED_LOAD_1465`. Do not claim stability until clean after this.

Next:
1. Revert or retune the water color/material response away from acid green and black smears.
2. Remove debug artifacts without destroying ocean color and material credibility.
3. Do not use saturation/color grading to fake richness.
4. Restore/produce complete proof packet: surface, shoreline close, underwater 0-5 m, underwater 20-50 m, Aegir/celestial, regression low oblique.
5. Fix or explicitly prove clear the null/forced-load runtime fault.

Acceptance floor remains Subnautica-level or better for surface and photic shallows.
