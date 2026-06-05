# Unity Owner Steer 2026-06-04 - 1468 Geometry Packet Reject

Target thread: `Продолжить работу по логам`
Mode: active-run steer. Use `Ctrl+Enter` if the current Codex run is still active.

Evidence reviewed:
- `Docs/Screenshots/MCP/h8_1468_surface_coast_aegir_ui_off.png`
- `Docs/Screenshots/MCP/h8_1468_shoreline_close_1m.png`
- `Docs/Screenshots/MCP/h8_1468_underwater_0_5m.png`
- `Docs/Screenshots/MCP/h8_1468_underwater_20_50m_route.png`
- `Docs/Screenshots/MCP/h8_1468_regression_low_oblique.png`
- `Docs/AgentLogs/UnityEditor_visual_audit_restart.log`

1468 is NOT accepted.

Good:
- A complete packet was produced, including underwater.
- Aegir is visible and large.
- Some surface color is less acid-green than 1466.

Reject:
- The packet proves a severe geometry/composition failure, not acceptance.
- Surface/coast/Aegir capture shows broken horizontal slices/black slab artifacts at the horizon and waterline. This reads like clipped geometry, disabled shell pieces, or bad camera/waterline setup.
- Shoreline close capture has huge black sliced platforms, floating/isolated columns, visible hard planes, and a striped/aliased water/contact artifact. This is not authored coastline.
- Underwater 0-5 m capture is nearly empty and sliced by large flat planes. It does not prove photic shallows.
- Underwater 20-50 m route appears duplicated/same broken frame as 0-5 m: large overhead plane, horizon slice, flat seabed, missing route geometry, no fauna/flora/detail, no credible depth composition.
- Regression low oblique still shows primitive platforms/slabs and flat green-blue water. The shore is not Subnautica-level or close.
- Do not continue by adding more props onto this broken shell. Fix visibility, camera/waterline, object enablement, material errors, and production mesh route first.
- The previous runtime fault remains unaccepted unless a clean current console/runtime proof is included. The log also still contains MCP WebSocket connection errors; if HTTP transport is healthy, state that explicitly and do not let WebSocket spam mask real Unity errors.

Immediate required correction:
1. Stop treating 1468 as a visual pass. It is a scene composition/geometry failure.
2. Identify the owner of the sliced black slabs/platforms and the huge overhead/underwater planes. Disable only proven debug/proxy objects; do not delete production candidates without reference proof.
3. Verify camera positions and clipping for surface, shoreline, underwater 0-5 m, and 20-50 m. The underwater views must actually be underwater and must not be intersecting terrain/ocean shell planes.
4. Verify active terrain/water/shore renderers and material errors before capture. If material/property errors exist, fix or rollback the bad assignment before claiming visuals.
5. Keep Aegir large and atmospheric, but capture it from a sane coastal composition without clipping slabs.
6. Restore credible ocean response: no neon fill, no black smear slabs, no hard rectangular shore bands.
7. Re-capture a clean packet only after the geometry and material state is sane:
   - surface coast/Aegir, UI off
   - shoreline close at waterline
   - underwater 0-5 m
   - underwater 20-50 m route
   - regression low oblique
   - console/runtime fault proof

Acceptance floor remains Subnautica-level or better for surface, coastline, ocean surface, and photic shallows. 1468 is far below that floor.
