# Domain Ephemeris Route Table

Status: production-facing draft pending native localization.

Route bands, public lane names and lower Deep Reach office surfaces.

## Packets

- `P161_DOMAIN_DISTANCE_SCALE` - Domain Distance Scale: Domain Distance Scale gives the wiki a clean non-FTL map language.
- `P162_DOMAIN_POPULATION_AUTHORITY_SCALE` - Population And Authority Scale: Population And Authority Scale describes human space by pressure routes instead of encyclopedia bloat.
- `P163_PUBLIC_ROUTE_NAMES` - Public Route Names: Public Route Names provides web/wiki-ready lane labels for the sparse frontier.
- `P164_TRANSIT_DURATION_BANDS` - Transit Duration Bands: Transit Duration Bands locks a usable timing grammar for route cards and articles.
- `P165_DEEP_REACH_SUBOFFICE_REGISTRY` - Deep Reach Suboffice Registry: Deep Reach Suboffice Registry makes legal terminals and site articles consistent.

## Runtime Rule

Authoring/export source only. Runtime consumes baked static-data rows, packet hashes and string-pool offsets.
No runtime markdown parsing, JSON parsing, live translation or scene search.
