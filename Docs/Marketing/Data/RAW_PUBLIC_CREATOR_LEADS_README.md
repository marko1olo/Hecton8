<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# Raw Public Creator Leads Readme

Status: data dictionary / not outreach-ready
Generated: 2026-05-18
Runtime impact: none

## Files

| File | Rows | Purpose |
|---|---:|---|
| `RAW_PUBLIC_CREATOR_LEADS_2026-05-18.csv` | 7155 | Raw public rows from LetsPlayIndex game/category pages. Multiple rows per creator are expected. |
| `UNIQUE_CREATOR_VERIFICATION_QUEUE_2026-05-18.csv` | 4970 | Deduplicated channel queue with segment, pitch angle, pitch stub, risk note, and next action. |
| `PRIORITY_CREATOR_SHORTLIST_FROM_RAW_2026-05-18.csv` | 250 | Highest-priority first verification batch sorted by repeated cross-game appearance and public metric. |
| `RAW_LEAD_FETCH_LOG_2026-05-18.csv` | 117 fetches | Fetch audit. 102 OK, 15 rate-limited by HTTP 429. |
| `RAW_LEAD_SCRAPE_SUMMARY_2026-05-18.md` | summary | Human-readable scrape summary and top 50 sample. |

## Data Boundary

This data is prospecting fuel, not a contact database.

It does not prove:

- current activity;
- official YouTube/Twitch URL;
- public business email;
- willingness to cover indie games;
- sponsorship availability;
- language suitability;
- brand safety;
- consent to receive a pitch.

Do not send outreach from these CSVs directly.

## Column Meaning - Raw Rows

| Column | Meaning |
|---|---|
| `scraped_utc` | UTC time when the row was extracted. |
| `source_game` | Game page that produced the row. |
| `source_slug` | LetsPlayIndex slug used for reproducibility. |
| `source_segment` | Internal HECTON-8 adjacency label. |
| `source_surface` | Page type: latest LP, most LP views, or review channels. |
| `source_page` | Page tier: top-100, top-200, top-300, etc. |
| `source_url` | Exact index page. |
| `channel_name` | Public channel display name from the index. |
| `channel_profile_url` | LetsPlayIndex profile URL. Not a business contact route. |
| `country` | Country value exposed by index, or UNKNOWN. |
| `primary_metric_raw` | Subscriber/view metric as displayed by the index surface. |
| `latest_or_ranked_video_title` | Video title visible on the source row. |
| `published_or_rank_context` | Date/rank context visible on the source row. |
| `contact_route` | Always `UNKNOWN_VERIFY_FROM_CREATOR_PAGE` until manually checked. |
| `verification_status` | Always raw/not-contact-ready at scrape time. |

## Column Meaning - Unique Queue

| Column | Meaning |
|---|---|
| `source_games` | All source games where this channel appeared. |
| `raw_occurrences` | Number of times the channel appeared across source pages. Higher usually means better genre overlap. |
| `recommended_segment` | Pitch segment assigned from source-game overlap. |
| `pitch_angle` | Short internal angle. |
| `personalized_pitch_stub` | Per-lead draft seed. Must still be personalized to actual channel content and a real HECTON-8 asset. |
| `risk_notes` | Segment-specific risk. |
| `next_action` | Verification work required before outreach. |

## Verification Procedure

For each lead in the 250-row priority shortlist:

1. Open the LetsPlayIndex profile.
2. Find the creator's official YouTube/Twitch/site link only from public profile routes.
3. Check uploads within the last 90 days.
4. Check whether survival/horror/engineering content is still active.
5. Identify language and region.
6. Check brand safety: scams, hate content, key-reseller signals, AI spam, dead channel, stolen VODs.
7. Record official contact route only if public and creator-owned.
8. Assign one real HECTON-8 asset that matches their channel.
9. Rewrite `personalized_pitch_stub` into a one-sentence custom opener.
10. Keep raw queue rows out of the live CRM until a deliberate promotion occurs. When promoted, use only live CRM statuses from `Data/CREATOR_VERIFICATION_TEMPLATE.csv`: `VERIFY_BEFORE_CONTACT`, `NEEDS_ASSET`, `LOW_PRIORITY_VERIFY_LATER`, or `DO_NOT_CONTACT` before any send. Do not create `VERIFIED_NOT_CONTACTED` or copy raw queue states into the live CRM.

## Outreach Conversion Rules

A verified lead gets one of four pitch modes:

| Segment | Pitch mode |
|---|---|
| `direct_underwater_survival` | Compare by audience only, not by "killer" rhetoric. Lead with pressure/machinery/black water. |
| `survival_route_risk` | Lead with expedition loop, scarcity, return planning, and fair failure. |
| `engineering_base_systems` | Lead with base-as-machine, power, oxygen, pumps, pressure, and salvage logistics. |
| `abyss_horror_pressure` | Lead with instruments, sound, darkness, and systemic dread. |

## Scaling To 10,000+ Leads

Do not hammer public sites. Use staged crawls:

1. Add 5-10 adjacent game slugs.
2. Run `scrape_letsplayindex_public_leads.ps1 -MaxTopPage 300`.
3. Wait before deeper pages if HTTP 429 appears.
4. Merge CSVs by `channel_profile_url`.
5. Verify no more than the top 250 in a source-backed sprint, and only after asset proof exposes a segment gap the live CRM cannot cover.
6. Promote no more than 50 leads per week into gated CRM review states; do not label them outreach-ready until contact, asset, build/demo, and send-log gates pass.

More leads are useless until verification labor catches up.
