# Controller Source/Bake Gate Plan P461-P491

Evidence class: STATIC_CONTROLLER_PLAN.
Runtime proof: absent.
Native localization proof: absent.
DataMonolith/h8bin proof: absent.
Publication proof: absent.

## Scope

This plan defines the next proof gates for packets P461-P491. It is not execution. It does not edit source CSV, route cards, generated pages, h8bin, Unity assets, runtime scripts, or publication outputs.

## Current Evidence Classes

| Packet range | Current class | Meaning |
|---|---|---|
| P461-P464 | SOURCE_ADMITTED_STATIC_AUDITED | Source structures include the rows and passed static source audit. |
| P465-P487 | STATIC_SOURCE_CANDIDATE | Candidate JSON/manifest bundles RS094-RS096 exist and passed shape audit. |
| P488-P491 | STATIC_DOC_ACCEPTED | Production packets exist and passed static text-shape audit. RS097 owner is active. |

## Gate Order

1. Packet-shape gate:
   - 15 locale rows per packet.
   - 1 English authority row.
   - 14 draft translation rows.
   - No replacement characters, mojibake markers, malformed locale headings, forbidden static-proof wording, or positive proof overclaims.

2. STATIC_SOURCE candidate gate:
   - Packet JSON parses.
   - Manifest parses.
   - Packet and manifest counts match.
   - Required localized surface keys exist per locale.
   - Authoring-only runtime contract flags remain false for engine/data claims.
   - Candidate does not include packet IDs outside assigned scope.

3. Source-admission planning gate:
   - Explicit source/bake owner assigned.
   - Process gate clean.
   - Rollback scope named.
   - Exact target files named.
   - Route-card/source-row/generated-page/hash outputs planned together.
   - Native localization boundary kept as draft unless proof exists.

4. Source CSV / route-card / generated-page static gate:
   - Source CSV row count and packet IDs match candidate.
   - Route cards only exist for source-admitted packet IDs.
   - Generated page/hash output matches source rows.
   - No runtime claims from static generated artifacts.

5. Import/bake gate:
   - Approved authoring bridge runs.
   - Static data payload generated through the project route.
   - Import/bake log retained.
   - h8bin payload presence and checksum recorded.
   - No runtime claim until engine boot/load proof exists.

6. Runtime/UI proof gate:
   - Unity import/console proof.
   - String-pool lookup proof.
   - Surface binding proof for scanner, terminal, codex, subtitle/audio, dossier, and public/archive surfaces as applicable.
   - GC/frame/memory proof for player-facing surfaces.
   - Native/font/layout proof before non-English surface lock.

## Low / Middle / High / Ultra Consequences

Low/Compact:
- Scanner/codex short text only after bake and binding proof.
- No extended archive/caption crosslinks.

Middle:
- Terminal body and one field-note variant can be enabled after layout proof.

High:
- Audio subtitle, dossier crosslink, and multiple evidence queue rows can be enabled after runtime proof.

Ultra:
- Public archive, index, caption chain, extended dossier variants, and contradiction links can expand after native/publication proof.

These are presentation-density consequences only. Packet identity, LocID identity, source authority, unlock route, spoiler byte, custody truth, and save identity must not change by quality lane.

## Rejection Rules

- Reject any attempt to use Markdown or candidate JSON as runtime data.
- Reject route cards for packet IDs absent from source rows.
- Reject generated pages for packet IDs absent from source rows.
- Reject non-English lock without native and layout proof.
- Reject h8bin or runtime claims from static docs, source candidates, or local JSON parse alone.
- Reject source admission while Unity/build/import processes are already active unless a separate owner and clean gate explicitly cover it.
