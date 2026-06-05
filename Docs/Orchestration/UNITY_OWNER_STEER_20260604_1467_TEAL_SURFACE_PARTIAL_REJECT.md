# Unity Owner Steer 2026-06-04 - 1467 Partial Reject

Target thread: `Продолжить работу по логам`
Mode: active-run steer, not a new task wave.
Do not stop the current Unity proof loop. Use this as acceptance feedback for the current visual/runtime pass.

Evidence reviewed:
- `Docs/Screenshots/MCP/h8_1467_surface_teal_ui_off.png`
- `Docs/Screenshots/MCP/h8_1467_shoreline_teal_1m.png`
- `Docs/Screenshots/MCP/h8_1467_regression_teal_low_oblique.png`
- `Docs/AgentLogs/UnityEditor_visual_audit_restart.log`

Good:
- The 1466 acid-green pass was improved.
- Obvious surface placeholder fish/silhouette clutter remains removed.
- The primitive small brown celestial dot/sun artifact appears disabled in the 1467 packet.
- Sky/cloud coverage is legible and no longer black/noir at the surface.

Reject:
- 1467 is still not accepted.
- Ocean color is still over-saturated teal/green and reads as a flat tinted plane. Bright surface water is required, but it must read as credible premium ocean water, not a posterized color fill.
- Do not use saturation/color grading as a substitute for material response, wave scale, foam, caustics, depth tint, or shoreline breakup.
- Shoreline/terrain still reads as a grey/yellow/black striped procedural terrain shell. It does not read as authored wet basalt/coastal geology.
- The island silhouette and waterline still lack organic breakup, wet/dry material transition, believable contact foam/salt, coastal stones, and close-range detail.
- No 1467 underwater proof was included. The 0-5 m and 20-50 m photic routes remain unproven after the previous underwater reject.
- Runtime stability is still not accepted until the earlier repeated `ArgumentNullException` and `H8_PLAYMODE_EXIT_AFTER_INVALID_FORCED_LOAD_1465` are either fixed or explicitly proven absent in a clean current run.
- Aegir/celestial state is not fully proven in 1467. If Aegir is present, capture the correct horizon/atmosphere composition and prove it is not a primitive billboard/dot artifact.

Next required pass:
1. Retune water away from neon teal/green. Use physically credible shallow tropical blue/blue-green with material detail, not global saturation.
2. Preserve the removal of placeholder fish/silhouettes and the primitive celestial artifact.
3. Replace or materially improve the terrain shell appearance. If final generated materials are not ready, use a controlled interim material that does not look striped, muddy, or procedural-placeholder.
4. Add credible shoreline breakup: wet basalt contact, foam/salt mask, small stones/ledges, and non-uniform waterline transition.
5. Produce the complete proof packet in one current run:
   - surface wide, UI off
   - shoreline close, waterline height
   - underwater 0-5 m
   - underwater 20-50 m route
   - Aegir/celestial horizon composition
   - regression low oblique
   - clean console/runtime fault proof
6. Keep screenshots under `Docs/Screenshots/MCP` or another non-Assets folder. Do not reintroduce the `Assets/Screenshots` import loop.

Acceptance floor remains Subnautica-level or better for surface, coastline, ocean surface, and photic shallows.
