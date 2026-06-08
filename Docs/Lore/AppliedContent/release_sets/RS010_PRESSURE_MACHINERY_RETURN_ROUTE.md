# RS010_PRESSURE_MACHINERY_RETURN_ROUTE

Status: production article source candidate pending importer admission, route-card bake, native localization review and Unity placement.

Purpose: push lore toward playable pressure machinery and return-route decisions: pump rooms, hatch seals, cable repair scars, sonar return beacons and custody-aware salvage tools. This set is built for scannables, terminals, route cards, screenshot planning and future scene bindings, not broad exposition.

Packets:
- `P046_PUMP_ROOM_HANDSHAKE`: a local pump can dry one room while loading the return corridor.
- `P047_HATCH_SEAL_LEDGER`: hatch seals preserve pressure cuts, closure direction and evacuation override evidence.
- `P048_CABLE_SPLICE_SCAR`: early Atlas-6 repair appears useful before it becomes visibly wrong.
- `P049_SONAR_RETURN_ROUTE`: stale return beacons force a fresh ping before cargo extraction.
- `P050_SALVAGE_TOOL_CUSTODY`: salvage tools record cuts, repairs and samples as claim evidence.

Runtime boundary:
- Bind to physical pump rooms, hatch frames, cable junctions, sonar pylons and salvage-tool lockers.
- Prefer scannable/terminal unlocks over abstract codex unlocks.
- Do not parse markdown or JSON at runtime.
- Do not live-translate packet rows at runtime.
- Do not scene-search this source set directly.
- Bake through the AppliedLore importer only after importer admission and route-card evidence exist.
- Keep `runtime_ready`, `native_localization_ready`, `generated_page_ready`, `unity_placement_ready` and `publication_ready` false until there is fresh proof for each gate.
