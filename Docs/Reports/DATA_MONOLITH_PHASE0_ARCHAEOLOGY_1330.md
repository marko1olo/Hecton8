# DATA_MONOLITH_PHASE0_ARCHAEOLOGY_1330

Status: STATIC_SOURCE / PENDING UNITY VERIFICATION
GeneratedUtc: 2026-05-26T10:01:34.0767832Z

Config-like files scanned: 354
Mapped current DataMonolith sources/templates: 286
Unmapped cross-domain source data: 14
DTO explicit-layout records parsed: 32
Active blob: version 2, schema 0x33313331, sections 26, bytes 1064384

Findings:
- Current active blob/source constants are format version 2 and schema 0x33313331.
- Active DATA_MONOLITH_H8BIN_SPEC.md still lists format version 1 and schema 0x58303032; doc update required before final report.
- The current DataMonolith bake lane maps Data/Balance recognized tables to BufferID.DataMonolithPayload sections, not one BufferID per table.
- Cross-domain Assets/_SourceData CSV files are not automatically DataMonolith-owned; route cards/owners are required before migration.
- No dotnet build was launched in Phase 0.
