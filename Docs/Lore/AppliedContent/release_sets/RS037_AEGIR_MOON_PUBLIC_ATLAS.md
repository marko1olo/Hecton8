# Aegir Moon Public Atlas

Status: production-facing draft pending native localization.

Moon naming policy, orbital hazards, moon-role ledger, ephemeris tuning and spoiler-safe public atlas rules.

## Packets

- `P181_MOON_NAME_LOCK_POLICY` - Moon Name Lock Policy: Moon Name Lock Policy explains why Aegir moon labels are adjustable while their route functions remain fixed.
- `P182_HECTON8_ORBITAL_HAZARD_TABLE` - HECTON-8 Orbital Hazard Table: HECTON-8 Orbital Hazard Table turns orbital mechanics into extraction pressure.
- `P183_AEGIR_MOON_LEDGER_ROLE_TABLE` - Aegir Moon Ledger Role Table: Aegir Moon Ledger Role Table summarizes the moon ladder around HECTON-8.
- `P184_RAN_AEGIR_EPHEMERIS_TUNING_RULE` - Ran Aegir Ephemeris Tuning Rule: Ran Aegir Ephemeris Tuning Rule separates stable lore bands from future numeric celestial tables.
- `P185_MOON_ROUTE_ARTICLE_SPOILER_BOUNDARY` - Moon Route Article Spoiler Boundary: Moon Route Article Spoiler Boundary defines safe public wiki coverage for Aegir moons.

## Runtime Rule

Authoring/export source only. Runtime consumes baked static-data rows, packet hashes and string-pool offsets.
No runtime markdown parsing, JSON parsing, live translation or scene search.
