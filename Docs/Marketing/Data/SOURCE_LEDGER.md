# Marketing Source Ledger

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## R4 Interior Actuality Boundary

This document is active only as static documentation/source/platform-orientation. Current authority is `AGENTS.md`, `.agents-skills`, `Docs/Actual Domains of Project.txt`, current source files, current official platform rules, current verification artifacts, and the latest DOC_GLOBAL reports.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, public Steam page, public demo, wishlist performance, creator outreach readiness, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters, platform rules, dates, and marketing claims inside this file are subordinate to fresh official sources and current project proof.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Status: living source index

## Evidence Classes

| Class | Meaning |
|---|---|
| `OFFICIAL_PLATFORM_DOC` | Steamworks, YouTube, FTC, TikTok or similar primary documentation. |
| `PUBLIC_CREATOR_PAGE` | Public YouTube/Twitch/site page. Fit only; no contact or availability assumed. |
| `THIRD_PARTY_INDEX` | LetsPlayIndex, SteamDB, Twitch tracker, press lists. Useful seed, must verify. |
| `COMMUNITY_SIGNAL` | Reddit, Steam reviews, Discord/forum posts. Directional, not proof alone. |
| `INTERNAL_DOC` | HECTON-8 docs, design notes, status/rationale logs. |
| `UNVERIFIED_LEAD` | A name/channel found in public data but not yet checked for fit/recent activity. |

## Platform / Compliance Sources

- Steam Wishlists: https://partner.steamgames.com/doc/marketing/wishlist?language=english
- Steam Next Fest: https://partner.steamgames.com/doc/marketing/upcoming_events/nextfest?language=english
- Steam Store Assets: https://partner.steamgames.com/doc/store/assets?language=english
- Steam Trailers: https://partner.steamgames.com/doc/store/trailer?language=english
- Steam Early Access: https://partner.steamgames.com/doc/store/earlyaccess
- Steam Keys: https://partner.steamgames.com/doc/features/keys
- FTC Endorsement Guides: https://www.ftc.gov/business-guidance/resources/ftcs-endorsement-guides-what-people-are-asking
- YouTube Paid Promotions: https://support.google.com/youtube/answer/10588440?hl=en
- TikTok Branded Content Policy: https://www.tiktok.com/legal/page/global/bc-policy/en

R18 official-source check on 2026-05-18: Next Fest eligibility and Early Access rules are external platform facts, not project facts. Treat the links above as the only authority before committing money, dates, keys, registration, or public launch language.

SHINOBU_81 asset-check addendum on 2026-05-18: `Steam/STEAM_PAGE_ASSET_REQUIREMENTS_CHECKLIST.md` records planning sizes and trailer/page structure from Steamworks documentation. Recheck `https://partner.steamgames.com/doc/store/assets?language=english` and `https://partner.steamgames.com/doc/store/trailer?language=english` immediately before final export because platform specs can change.

SHINOBU_81 mass-lead addendum on 2026-05-18: `AgentOps/scrape_letsplayindex_public_leads.ps1` extracted 7155 raw public rows and 4970 unique LetsPlayIndex channel profiles from Subnautica and adjacent survival/horror/engineering game pages. Fetch audit: 102 OK pages, 15 HTTP 429 rate-limited pages. The output is `THIRD_PARTY_INDEX` prospecting data, not contact permission.

## Creator Lead Sources

- LetsPlayIndex Subnautica channel views: https://www.letsplayindex.com/games/subnautica-2018/most-lets-play-channel-views
- LetsPlayIndex Subnautica lets-play channels: https://www.letsplayindex.com/games/subnautica-2018/lets-play-channels
- LetsPlayIndex Subnautica review channels: https://www.letsplayindex.com/games/subnautica-2018/review-channels
- Public YouTube channel pages for each listed creator.
- Public Twitch channel pages for each listed streamer.

## Raw Lead Mining Notes

PowerShell successfully fetched LetsPlayIndex pages on 2026-05-18 and extracted public channel names/URLs from:

- `most-lets-play-channel-views`
- `top-200`
- `top-300`
- `top-400`
- `top-500`
- `lets-play-channels`
- `review-channels`
- `most-lets-play-videos`

The first extraction returned hundreds of public channel seed rows. These are not outreach-ready. They require:

1. current activity check;
2. language check;
3. audience fit check;
4. contact route check;
5. sponsorship/coverage policy check;
6. pitch angle assignment;
7. denylist/fraud check.

## Verification Rules

- Subscriber count: `UNKNOWN` unless read from a current public page.
- Email/contact: `UNKNOWN` unless read from creator's own public page or business profile.
- Sponsorship availability: `UNKNOWN` unless explicitly stated.
- Recent activity: verify manually before contact.
- Do not infer consent to pitch from channel existence.
- Do not scrape private Discords or gated communities.
- Do not use leaked emails or purchased lists.
