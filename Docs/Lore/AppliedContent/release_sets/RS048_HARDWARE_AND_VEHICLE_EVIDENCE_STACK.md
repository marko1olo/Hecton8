# Hardware And Vehicle Evidence Stack

Status: production-facing draft pending native localization.

Lock ship, capsule, fabricator, suit and pinger hardware as concrete playable evidence.

## Packets

- `P236_BLACK_KEEL_TENDER_INTERIOR_LIMITS` - Black Keel Tender Interior Limits: Black Keel Tender Interior Limits define the player's carrier as hard-sci-fi salvage infrastructure. The ship is not a heroic private vessel; it is a claim-pool tender built around sample custody, packet law and debt pressure.
- `P237_DROP_CAPSULE_DAMAGE_PARTS` - Drop Capsule Damage Parts: Drop Capsule Damage Parts removes convenient rescue logic from HECTON-8. The player is trapped because named systems failed: ascent sleeve, guidance gimbal, relay mast, compressor and heat tiles. Each failure maps to a future route, material or evidence object.
- `P238_P63_FABRICATOR_AUTHORITY_LIMITS` - P-63 Fabricator Authority Limits: P-63 Fabricator Authority Limits explain the first crafting station in HECTON-8. It makes the player competent quickly, but it refuses ascent-grade parts until the player earns pressure-rated materials, route stamps and deeper evidence.
- `P239_PRESSURE_SUIT_SERVICE_GRADES` - Pressure Suit Service Grades: Pressure Suit Service Grades keep HECTON-8's gear progression hard-sci-fi. Better suits are not colored armor tiers; they are service records, seal proof, scrubber capacity, thermal margins and contamination limits.
- `P240_SONAR_PINGER_ROUTE_BEACONS` - Sonar Pinger Route Beacons: Sonar Pinger Route Beacons are a signature HECTON-8 object: cheap, physical, acoustic and morally loaded. They help the player map, return, tag evidence and negotiate rescue, while also announcing presence to the ocean.

## Runtime Rule

Authoring/export source only. Runtime consumes baked static-data rows, packet hashes and string-pool offsets.
No runtime markdown parsing, JSON parsing, live translation or scene search.
