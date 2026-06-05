# STEER_1474_FULL_PACKET_REJECT_FALSE_VIEWS

Target: `Продолжить работу по логам` Unity owner.
Date: 2026-06-04.
Evidence:
- `Docs/Screenshots/MCP/h8_1474_surface_coast_aegir_ui_off.png`
- `Docs/Screenshots/MCP/h8_1474_shoreline_close_1m.png`
- `Docs/Screenshots/MCP/h8_1474_underwater_0_5m.png`
- `Docs/Screenshots/MCP/h8_1474_underwater_20_50m_route.png`
- `Docs/Screenshots/MCP/h8_1474_aegir_celestial_long.png`
- `Docs/Screenshots/MCP/h8_1474_regression_low_oblique.png`
- `Docs/AgentLogs/UnityEditor_visual_audit_restart_1474b.log`

Verdict: REJECTED.

Primary reject:
- `FALSE_LABEL`: the six files are visually the same surface/coast/Aegir setup with small camera/FOV shifts.
- `h8_1474_underwater_0_5m.png` does not prove underwater state.
- `h8_1474_underwater_20_50m_route.png` does not prove 20-50 m route.
- `h8_1474_shoreline_close_1m.png` is not a 1 m shoreline close proof.

Freshness / evidence rejects:
- no manifest/checksum/camera/depth/quality/toggle file exists for this packet;
- log is newer than screenshots but dirty:
  - force recompile/domain reload,
  - Asset Pipeline Refresh,
  - old `WeatherEvents` persistent leak stacks,
  - MCP WebSocket warnings,
  - compile/import events.
- `Unity.ILPP.Runner` was still active after packet creation.

Visual rejects visible in the packet:
- no believable shoreline foam/wet contact;
- no underwater caustics/particles/volume proof;
- water remains dark/green and reads flat;
- shoreline/terrain remains blackened and weak;
- Aegir remains dirty green/black with crude vertical/seam/stripe artifacts.

Required next action:
1. Stop producing more surface-lookalike packet views.
2. Fix capture harness/camera route first:
   - prove actual underwater state/depth in metadata and by image;
   - move camera below water for `0-5m`;
   - move camera to actual `20-50m` route, not surface horizon;
   - move camera to real 1 m shoreline close.
3. Do not claim visual progress until the packet has six distinct route-correct views and clean manifest/log.
4. Before another visual packet, follow Batch25 synthesis:
   - clean `WeatherEvents` leak proof;
   - resolve `HectonCelestialEngine.sunVisualTransform`;
   - quiet compile/import/ILPP;
   - Batch24 slab/caustic isolation;
   - `Ocean.mat` clipping / `Ocean_UnderwaterCurtain.mat` risk check.

Acceptance remains Subnautica-floor or better for surface/photic shallows. This packet is not close.
