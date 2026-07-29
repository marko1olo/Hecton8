---
packet_id: P501_EVIDENCE_MARKET_CLEANUP_BID_BRIDGE
release_set_id: RS100_PUBLIC_EVIDENCE_CLEANUP_CONFLICT_BRIDGE
article_id: EVIDENCE_MARKET_CLEANUP_BID_BRIDGE
unlock_id: unlock.p501_evidence_market_cleanup_bid_bridge
poi_tags: poi.public_archive_receiver_shelf;poi.evidence_market_terminal
biome_tags: biome.deep_archive
locale: en_US
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "Evidence Cleanup Bid"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: source_authority
localization_flags: 0
prereq_packet_ids: P500_PUBLIC_ARCHIVE_RECEIVER_AMBIGUITY_BRIDGE
next_packet_ids: P502_CLAIMANT_SAFE_SUMMARY_CONFLICT_BRIDGE
---

# Evidence Cleanup Bid

Cleanup bid EB-31, filed against evidence route 9-K, escrow held.

  ORIGINAL LABEL: worker return, partial
  CLEANED LABEL: cargo fitness, partial
  ESCROW HOLD: opened before the relabel

A cleanup bid is a purchase order against an evidence route. It can ask a broker to dry a tag, normalise a label, move a fragment into a salvage lot, delay publication, or translate a worker name into a payout category.

Read it by sequence. A cleaned label appearing before payment can be ordinary archive handling. Payment arriving before the relabel means somebody bought the route change.

Keep the original label beside the cleaned one. Archive only the clean label and the archive becomes part of the cleanup.

## Scanner

CLEANUP BID // Paid request against evidence route. Required: original label, bid origin, escrow hold, handler account, cleaned label, custody transfer, object route.

## Terminal

EVIDENCE CLEANUP BID
Do not treat payment as verdict.
Payment before relabel = purchased route change.
Relabel before payment = possible archive handling.
Required next proof: escrow hold, handler account, custody transfer, old label, cleaned label, object route.
Action: preserve both labels until the object route is resolved.

## Audio

Payment found before the label changed. Keep the old name on the screen.

## Field Note

Never archive the clean label alone. The old label and the payment clock are the evidence.

<!-- In-Game Wiki; generated from P501_EVIDENCE_MARKET_CLEANUP_BID_BRIDGE/en_US. -->
