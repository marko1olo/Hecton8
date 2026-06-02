# RS010 - Pressure Machinery / Return Route

Status: production-facing AppliedContent release set; non-EN/RU localization is draft-filled and requires native pass.

Purpose: push lore toward the actual playable surface: machines, hatches, cable scars, sonar return routes and tool custody. This set is built for scannables, terminals, screenshot planning and future scene bindings, not broad setting exposition.

Packets:
- P046_PUMP_ROOM_HANDSHAKE: pump-room readability, pressure cost and repair tradeoff.
- P047_HATCH_SEAL_LEDGER: hatch seals as route evidence and timestamped decisions.
- P048_CABLE_SPLICE_SCAR: early Atlas repair trace before biological escalation.
- P049_SONAR_RETURN_ROUTE: return route degradation as navigation pressure.
- P050_SALVAGE_TOOL_CUSTODY: tool use as claim evidence and ending-economy pressure.

Runtime use:
- Bind to physical pump rooms, hatch frames, cable junctions, sonar pylons and salvage-tool lockers.
- Prefer scannable/terminal unlocks over abstract codex unlocks.
- Do not parse markdown or JSON at runtime; bake packet JSON through the AppliedLore importer.
