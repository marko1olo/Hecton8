# UNITY OWNER STEER - 1474 Diagnostic Reject

Date: 2026-06-04

Reviewed:
- `Docs/Screenshots/MCP/h8_1474_diag_surface_from_mcp.png`
- `Docs/Screenshots/MCP/h8_1474_diag_shore_foam_from_mcp.png`
- `Docs/Screenshots/MCP/h8_1474_diag_underwater_route_from_mcp.png`
- `Docs/AgentLogs/UnityEditor_visual_audit_restart_1474b.log`

Verdict: diagnostic progress, not acceptance.

Good:
- Surface water color is no longer acid green.
- Surface is brighter and calmer.
- Compile after celestial MPB hardening succeeded.

Reject:
- Only 3 diagnostic screenshots, not a complete six-view packet.
- Log tail is older than screenshots; no clean post-capture runtime tail.
- No metadata/checksum manifest.
- Shoreline still has almost no believable foam, no wet contact breakup, and water cuts rock like a clean plane.
- Underwater is still hard reject: huge horizontal cut / blue wall, empty grey seabed, weak/no haze, weak/no caustics, no particles, pasted-looking rocks.
- Terrain/shore still reads as shell/prototype, not premium photic route.
- Aegir/celestial proof missing from this diagnostic set.

Next exact focus:
1. Kill/replace the horizontal underwater cut. If `H8_FloorCausticSoft_1443` is the sheet, make it subtle broken caustic lace or disable it for proof; do not leave a broad opaque plane.
2. Add real shoreline contact foam using only a narrow/organic route. Do not use old lace/blob/unlit/broken/ribbon/rib routes.
3. Add actual underwater proof cues: depth haze, particulate/marine snow, seabed/rock detail, route cue, caustic receiver response.
4. Produce complete 1474+ packet with metadata and clean post-capture log tail.

Reject codes:
- `MISSING_VIEW`
- `STALE_LOG`
- `NO_METADATA`
- `NO_FOAM_CONTACT`
- `UNDERWATER_CUT_PLANE`
- `EMPTY_SEABED`
- `WEAK_CAUSTICS`
- `NO_PARTICULATE_HAZE`
