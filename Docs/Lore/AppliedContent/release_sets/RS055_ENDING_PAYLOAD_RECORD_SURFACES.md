# Ending Payload Record Surfaces

Status: production-facing draft pending native localization.

Lock concrete ending records for material, partial, public, severance and quarantine/preserve outcomes.

## Packets

- `P271_MATERIAL_PAYOUT_RECORD` - Material Payout Record: Material Payout Record records the paid exit receipt: coordinates and sample accepted, lien reduced, worker evidence left outside notarized custody.
- `P272_PARTIAL_RETURN_RECORD` - Partial Return Record: Partial Return Record logs temporary Black Keel pickup without contract closure, preserving scan memory, open evidence and same-seed return authority.
- `P273_PUBLIC_LEDGER_RECORD` - Public Ledger Record: Public Ledger Record tracks a custody break: a coordinate-redacted evidence packet reaches public receipt before Deep Reach can close the archive.
- `P274_ATLAS_SEVERANCE_RECORD` - Atlas Severance Record: Atlas Severance Record lists the cut links, failing micronodes and denied Deep Reach route before any receiver labels the act mercy, theft or damage.
- `P275_PRESERVE_QUARANTINE_RECORD` - Preserve Quarantine Record: Preserve Quarantine Record holds the payload out of Deep Reach custody while Atlas damage, ecology protection and future filings remain active.

## Runtime Rule

Authoring/export source only. Runtime consumes baked static-data rows, packet hashes and string-pool offsets.
No runtime markdown parsing, JSON parsing, live translation or scene search.
