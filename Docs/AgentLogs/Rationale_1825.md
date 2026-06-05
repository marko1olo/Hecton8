# Rationale 1825

## Queue Boundary

The queue is prioritized, not exhaustive. Task 1825 asked for first-route and early-game placement work, so the queue samples packets with direct route beats, POI tags, and prior 1773/1774/1775/1778 handoff evidence instead of duplicating all 460 packet IDs.

## Locale Policy

`en_US` is treated as static source candidate where 1820 supports it. Non-English rows remain draft/native-review pending. P151-P155 carry a specific ru_RU wiki/site status-drift blocker from 1820 and were marked accordingly.

## Authoring Rows

P316, P320, P432, P433, and P446 were included as blocked authoring/specification rows because they guide placement and surface rules, but they must not ship as player-facing text.

## Blocked Content

P007 was allowed only for scanner placement because 1820 blocks its in-game wiki surface with `ai_meta_phrase` residue. P457-P460 were blocked entirely for public/game placement because 1820 reports production-residue terms in core public/game surfaces.

