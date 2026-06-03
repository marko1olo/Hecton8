# RS029_ROUTE_TIME_DISTANCE_MODEL

Status: production-facing draft, pending native localization pass.
Runtime contract: authoring source only; runtime must consume baked static data/string-pool rows.

Purpose: locks practical route-distance, freight, crew and message-delay scale so Aegir remains hard-sci-fi reachable but not rescuable.

## Packets

- `P141_RAN_AEGIR_DISTANCE_MODEL` - Ran-Aegir Distance Model: Ran-Aegir Distance Model gives site/wiki text a clean scale: reachable by infrastructure, unreachable by sympathy.
- `P142_PROBE_PACKET_TRAVEL_TIMES` - Probe Packet Travel Times: Probe Packet Travel Times explain how Aegir became a claim before it became a place.
- `P143_HEAVY_FREIGHT_STAGING_TIME` - Heavy Freight Staging Time: Heavy Freight Staging Time keeps the no-FTL timeline plausible without convenient rescue ships.
- `P144_HUMAN_CREW_ROTATION_TRANSIT` - Human Crew Rotation Transit: Human Crew Rotation Transit is the human side of no-FTL: labor becomes route debt.
- `P145_RELAY_MESSAGE_LAG` - Relay Message Lag: Relay Message Lag makes communication a route system, not a magic voice channel.

## Production Use

- Scanner and terminal snippets are short enough for diegetic UI.
- In-game wiki and external-site fields are generated from the packet bundle.
- Route cards connect packet IDs to depth windows, replay axes and ending pressure.
- Binding maps provide future Unity/DataMonolith placement targets without runtime markdown parsing.
