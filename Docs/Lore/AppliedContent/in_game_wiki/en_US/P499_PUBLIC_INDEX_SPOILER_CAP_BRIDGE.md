---
packet_id: P499_PUBLIC_INDEX_SPOILER_CAP_BRIDGE
release_set_id: RS099_PUBLIC_EVIDENCE_GOVERNANCE_BRIDGE
article_id: PUBLIC_INDEX_SPOILER_CAP_BRIDGE
unlock_id: unlock.p499_public_index_spoiler_cap_bridge
poi_tags: poi.public_ledger_mirror;poi.tau_ceti_evidence_archive
biome_tags: biome.deep_archive
locale: en_US
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "Public Index Redaction Rule"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: source_authority
localization_flags: 0
---

# Public Index Redaction Rule

A public index row is an access-control record, not a summary. It may show the docket family, custody lane, proof type, redaction flag, scanner gate, and the next object needed to open the packet. That is enough to route a search without exposing the receiver.

The masked fields matter more than the title. Receiver chain, final coordinates, payload condition, Atlas response, ending pressure, and legal result stay behind the redaction seal until the route earns them. If the index prints a receiver address too early, it does not inform the case; it leaks the route to anyone watching the mirror.

## Scanner

PUBLIC INDEX REDACTION // Visible: docket family, custody lane, proof type, redaction flag, scanner gate, next proof object. Masked: receiver chain, coordinates, payload state, Atlas response, legal result.

## Terminal

PUBLIC INDEX REDACTION STAMP
Show: title, docket family, custody lane, proof type, redaction flag, scanner gate, next proof object.
Mask: receiver chain, final coordinates, payload state, Atlas response, ending pressure, legal result.
Failure: receiver address visible before custody threshold.
Action: keep route fields sealed until the required packet hash and object proof match.

## Audio

Index row is open. Receiver line is not. If you can see both, someone burned the route.

## Field Note

Use the index to choose the next proof object. Do not treat a visible category as custody, verdict, or destination.

<!-- In-Game Wiki; generated from P499_PUBLIC_INDEX_SPOILER_CAP_BRIDGE/en_US. -->
