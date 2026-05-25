# Press And Steam Curator Targets

Status: public seed map / not outreach-ready. Operational send status lives in `Press/PRESS_TARGET_VERIFICATION_TRACKER.csv` and `Press/STEAM_CURATOR_CANDIDATE_TRACKER.csv`; recheck same-day before outreach/key use and fill `send_route_class` / `reply_consent_provenance` fields before counting replies.
Date: 2026-05-18

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- current publication, curator, and Steamworks pages
- fresh outreach/key logs

No press contact route, curator eligibility, key policy state, demo approval, runtime build, Unity import, profiler, player-build, or marketing-performance proof is implied unless this document links a fresh evidence artifact. Rows are public target seeds and must be reverified before outreach or key distribution.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Hygiene Rules

- Prefer Steam Curator Connect over loose key drops.
- Never send bulk keys to unverifiable Gmail, Discord, or X DMs.
- Require visible publication/channel history matching PC indie/horror/survival.
- Track every key recipient.
- For curators, prioritize consistent review history and real off-Steam identity.
- Avoid "we review anything" pages.

## 2026-05-20 Tracker Status Boundary V0

Tracker `status` values are triage labels, not send permission. A row that says `READY_FOR_HUMAN_REVIEW_AFTER_PRESSKIT`, `READY_FOR_HUMAN_REVIEW_AFTER_PUBLIC_DEMO`, `CURATOR_CONNECT_AFTER_STEAM_PAGE_AND_BUILD`, or similar is still blocked until the named artifact exists and the route is rechecked the same day.

The tracker CSVs now carry a separate `send_permission_gate` field. Treat it as the machine-readable permission gate:

- current pre-asset values must begin with `BLOCKED_` or equal `DO_NOT_CONTACT_COMPETITOR`;
- no press row can send unless `send_permission_gate = ALLOW_PRESS_SEND_VERIFIED`;
- no curator row can receive a Curator Connect offer or other controlled send unless `send_permission_gate = ALLOW_CURATOR_SEND_VERIFIED`;
- `send_route_class` records route type before send; it is not permission by itself;
- `reply_consent_provenance` stays blank until a real reply exists.

Press send permission requires all of:

- `send_permission_gate = ALLOW_PRESS_SEND_VERIFIED`;
- the row has a current official route checked the same day;
- required asset/presskit/demo/Steam artifact exists;
- official project inbox custody passes;
- `send_route_class` is filled before send;
- `reply_consent_provenance` remains blank until a reply exists;
- no gameplay/pressure/route-risk proof is used unless the AB-009/KPI decision-read field source exists.

Curator send permission requires all of:

- `send_permission_gate = ALLOW_CURATOR_SEND_VERIFIED`;
- public Steam page and uploaded playable build exist;
- Curator Connect is used where possible instead of raw keys;
- first exposed screenshot set passes asset QA and claim checks;
- one agency/decision proof asset has AB-009/KPI decision-read fields if the message leans on gameplay/pressure/route-risk proof;
- `send_route_class` is filled before the Curator Connect offer or other controlled send;
- `reply_consent_provenance` remains curator/press-local unless explicit separate opt-in exists.

Invalid interpretation: treating `READY_FOR_HUMAN_REVIEW_AFTER_*` as `READY_TO_SEND`.

## Press / Newsletter / Showcase Targets

This table is seed navigation only. For any row with a tracker entry, use the tracker `last_checked`, `status`, `send_permission_gate`, `asset_required`, `send_route_class`, and `reply_consent_provenance` fields instead of this table before outreach or reporting.

| Segment | Name | Type | URL | Fit Reason | Contact Route | Risk Notes | Pitch Angle |
|---|---|---|---|---|---|---|---|
| PC press | PC Gamer | PC games press | https://www.pcgamer.com/about-pc-gamer/ | PC-first survival coverage. | Official about/contact page. | High volume. | Single-player underwater survival with systemic dread. |
| PC press | Rock Paper Shotgun | PC games press | https://www.rockpapershotgun.com/ | Strong PC indie readership. | UNKNOWN | Selective. | NASA-punk survival sim, single-player-first proof angle. |
| PC press | Eurogamer | Games press | https://www.eurogamer.net/ | Survival/indie features. | UNKNOWN | Broad console focus. | Atmospheric PC survival with authored systems. |
| PC press | GamesRadar+ | Games press | https://www.gamesradar.com/ | Future/PC coverage. | UNKNOWN | Broad. | Deep-sea horror survival reveal/demo. |
| PC press | GameSpot | Games press | https://www.gamespot.com/ | Major demo/news outlet. | UNKNOWN | Very competitive. | Pressure, darkness, machinery demo hook. |
| PC press | IGN | Games press | https://www.ign.com/ | Major trailer/demo reach. | UNKNOWN | Requires strong asset package. | Underwater survival horror with AA target. |
| PC press | VG247 | Games press | https://www.vg247.com/ | PC/indie news. | UNKNOWN | Needs newsworthiness. | Survival built around isolation, not crafting spam. |
| PC press | PCGamesN | PC games press | https://www.pcgamesn.com/ | PC survival audience. | UNKNOWN | SEO/news angle needed. | Adjacent underwater-survival audience, darker and single-player-first. |
| PC press | PC Invasion | PC games press | https://www.pcinvasion.com/ | PC indie/review fit. | UNKNOWN | Mid-tier reach. | Demo preview with systemic underwater hazards. |
| PC press | Shacknews | Games press | https://www.shacknews.com/ | PC legacy readership. | UNKNOWN | Needs concise pitch. | NASA-punk survival reveal. |
| PC press | Destructoid | Games press | https://www.destructoid.com/ | Indie/news coverage. | UNKNOWN | Broad taste. | Claustrophobic deep-sea survival hook. |
| PC press | TheGamer | Games press | https://www.thegamer.com/ | List/features potential. | UNKNOWN | Feature framing matters. | Underwater survival still has unused territory. |
| PC press | Hardcore Gamer | Games press | https://hardcoregamer.com/ | Reviews/previews. | UNKNOWN | Smaller staff. | Playable PC demo preview only after stable demo proof. |
| PC press | Wccftech Gaming | PC/tech press | https://wccftech.com/gaming/ | PC tech + games. | UNKNOWN | Tech specs must be real. | Scalable AA underwater rendering only with proof. |
| PC press | Digital Trends Gaming | Consumer tech/games | https://www.digitaltrends.com/gaming/ | PC/console news. | UNKNOWN | Broad consumer angle. | Survival horror with premium visual direction. |
| PC press | TechRadar Gaming | Tech/games press | https://www.techradar.com/gaming | PC hardware audience. | UNKNOWN | Needs visual/tech proof. | Looks expensive, but performance only if measured. |
| PC press | Game Rant | Games press | https://gamerant.com/ | Survival/horror SEO. | UNKNOWN | Headline-driven. | New darker underwater survival. |
| PC press | Siliconera | Games press | https://www.siliconera.com/ | Indie/horror posts. | UNKNOWN | Less PC-specific. | Odd, stylish survival reveal. |
| PC press | The Escapist | Games press/video | https://www.escapistmagazine.com/ | Indie/opinion fit. | UNKNOWN | Pitch must be sharp. | Single-player survival without live-service drag. |
| PC press | Niche Gamer | Games press | https://nichegamer.com/ | Niche PC/indie. | UNKNOWN | Audience tone varies. | Hardcore atmospheric survival sim. |
| Indie press | Indie Games Plus | Indie press | https://www.indiegamesplus.com/ | Direct indie discovery. | UNKNOWN | Small team. | Deep-sea survival with mood and systems. |
| Indie press | Alpha Beta Gamer | Indie demos | https://www.alphabetagamer.com/ | Demo discovery audience. | UNKNOWN | Demo quality critical. | Playable public demo for horror/survival fans. |
| Indie press | The Indie Game Website | Indie press | https://www.indiegamewebsite.com/contact/ | Public indie contact page. | Contact page. | Avoid spam. | AA-looking indie survival horror. |
| Indie press | Into Indie Games | Indie press | https://intoindiegames.com/contact/ | Public submissions/contact. | Contact page. | Smaller reach. | Preview/review when demo stable. |
| Indie press | IndieDB | Indie database/news | https://www.indiedb.com/contact | Indie project listing. | Contact/listing route. | Community-driven. | Project page + trailer/demo news. |
| Indie press | Indie Hive | Indie press | https://indie-hive.com/ | Indie reviews/features. | UNKNOWN | Smaller outlet. | Dark survival systems preview. |
| Indie press | The Indie Informer | Indie press/newsletter | https://the-indie-in-former.com/ | Curated indie coverage. | UNKNOWN | Selective. | Hands-on demo with clear hook. |
| Indie press | SuperIndieGames | Indie press | https://www.superindiegames.com/ | Indie discovery. | UNKNOWN | Verify activity. | Atmospheric survival horror demo. |
| Indie press | Hey Poor Player | Indie/game press | https://www.heypoorplayer.com/ | Indie reviews. | UNKNOWN | Mixed platform scope. | Deep-sea survival preview/review. |
| Indie press | Game If You Are | Indie PR/media | https://gameifyouare.com/ | Indie-focused editorial/PR knowledge. | UNKNOWN | May be PR not editorial. | Fit check for indie press campaign. |
| Indie press | Indie Retro News | Indie/retro press | https://www.indieretronews.com/ | PC indie discovery. | UNKNOWN | Retro bias. | Only if relevant retro/process angle exists. |
| Indie press | Indie Hell Zone | Indie curation | https://indiehellzone.com/ | Weird indie audience. | UNKNOWN | Taste-specific. | Bleak industrial underwater horror. |
| Indie press | Warp Door | Indie discovery | https://warpdoor.com/ | Short indie spotlights. | UNKNOWN | Often small/free games. | Striking demo GIF/trailer beat. |
| Indie press | Buried Treasure | Indie newsletter/site | https://buried-treasure.org/ | Deep indie discovery. | UNKNOWN | Paid/subscriber model. | Playable overlooked weird survival angle. |
| Horror press | Bloody Disgusting Games | Horror press | https://bloody-disgusting.com/video-games/ | Strong horror games vertical. | UNKNOWN | Needs horror-first framing. | Pressure, isolation, unseen abyss threat. |
| Horror press | Rely on Horror | Horror games press | https://www.relyonhorror.com/ | Survival horror specialist. | UNKNOWN | Strong genre standards. | Survival horror systems underwater. |
| Horror press | Dread Central Gaming | Horror press | https://www.dreadcentral.com/ | Horror audience. | UNKNOWN | Broader horror. | Deep sea as haunted-house replacement. |
| Horror press | DreadXP | Horror games site/publisher | https://www.dreadxp.com/ | Indie horror discovery. | UNKNOWN | Publisher conflict possible. | Demo news, not publishing ask. |
| Horror press | Horror Obsessive | Horror press | https://horrorobsessive.com/ | Horror features/games. | UNKNOWN | Broader media. | NASA-punk dread and survival pressure. |
| Horror press | Rue Morgue | Horror magazine/site | https://rue-morgue.com/ | Horror culture. | UNKNOWN | Games secondary. | Cinematic horror angle. |
| Horror press | Morbidly Beautiful | Horror site | https://morbidlybeautiful.com/ | Horror culture/games. | UNKNOWN | Smaller games footprint. | Deep-sea terror preview. |
| Horror press | The Horror Game Awards | Horror showcase/awards | https://thehorrorgameawards.com/ | Horror game audience. | UNKNOWN | Seasonal. | Awards/showcase consideration. |
| Newsletters | GameDiscoverCo | Industry/newsletter | https://newsletter.gamediscover.co/ | Steam discovery audience. | UNKNOWN | B2B more than consumer. | Discovery story if demo metrics exist. |
| Newsletters | How To Market A Game | Industry/newsletter | https://howtomarketagame.com/ | Steam marketing audience. | UNKNOWN | Not press coverage. | Learn/timing, not review pitch. |
| Newsletters | Indie Game Joe | Indie newsletter | https://indiegamejoe.com/ | Indie discovery. | UNKNOWN | Curated. | Upcoming demo spotlight. |
| Newsletters | Buried Treasure | Indie newsletter | https://buried-treasure.org/ | Deep indie discovery. | UNKNOWN | Taste-specific. | Unusual bleak survival angle. |
| Showcase | Steam Next Fest | Steam event | https://partner.steamgames.com/doc/marketing/upcoming_events/nextfest | Potential Steam demo funnel; recheck current Steamworks rules and compare against actual UTM/demo telemetry before prioritizing. | Steamworks registration. | Timing strict. | Final pre-launch demo beat. |
| Showcase | Steam themed fests | Steam events | https://partner.steamgames.com/doc/marketing/upcoming_events | Tag-specific Steam reach. | Steamworks opt-in. | Eligibility varies. | Survival/horror/sci-fi event. |
| Showcase | PC Gaming Show | PC showcase | https://www.pcgamingshow.com/ | PC-first showcase. | UNKNOWN | Competitive. | PC-first underwater survival reveal. |
| Showcase | Future Games Show | Digital showcase | https://www.futuregamesshow.com/ | Public showcase. | UNKNOWN | Competitive. | World premiere/demo drop. |
| Showcase | The MIX | Indie showcase | https://www.mediaindieexchange.com/ | Indie showcase operator. | UNKNOWN | Submission windows. | Hands-on indie survival demo. |
| Showcase | Guerrilla Collective | Indie showcase | https://www.guerrillacollective.com/ | Indie showcase audience. | UNKNOWN | Timing/fit varies. | Dark survival trailer/demo. |
| Showcase | Day of the Devs | Indie showcase | https://www.dayofthedevs.com/ | High-trust indie showcase. | UNKNOWN | Very selective. | Distinctive world + playable proof. |
| Showcase | Indie Horror Showcase | Horror showcase | https://www.indiehorrorshowcase.com/ | Exact horror demo fit. | UNKNOWN | Seasonal. | Underwater horror trailer/demo. |
YouTube rows are creator seeds only, not verified channel/contact records. Use the CRM row if present, or reverify official page/current activity before send.

| YouTube list | SplatterCatGaming | YouTube | https://www.youtube.com/@SplatterCatGaming | Strong indie survival demos. | YouTube About if public. | Do not DM keys blindly. | First 30 min demo coverage. |
| YouTube list | Alpha Beta Gamer | YouTube/site | https://www.youtube.com/@AlphaBetaGamer | Demo-first audience. | Site/channel route. | No fake exclusive. | Playable demo footage. |
| YouTube list | ManlyBadassHero | YouTube | https://www.youtube.com/@ManlyBadassHero | Indie horror audience. | YouTube About if public. | Very selective. | Atmospheric horror demo. |
| YouTube list | IGP | YouTube | https://www.youtube.com/@IGP | Survival/horror reach. | YouTube About if public. | Impersonators common. | Deep-sea survival episode. |
| YouTube list | Fooster | YouTube | https://www.youtube.com/@Fooster | Survival audience. | YouTube About if public. | Verify official route. | Solo survival exploration. |
| YouTube list | Drae | YouTube | https://www.youtube.com/@Drae | Simulation/survival audience. | YouTube About if public. | Needs gameplay readability. | Systems-driven survival demo. |
| YouTube list | Nookrium | YouTube | https://www.youtube.com/@Nookrium | Indie systems audience. | YouTube About if public. | Less horror. | Resource systems and exploration. |
| YouTube list | Wanderbots | YouTube | https://www.youtube.com/@wanderbots | Indie discovery. | YouTube About if public. | High inbound volume. | Steam demo first look. |
| YouTube list | Best Indie Games | YouTube | https://www.youtube.com/@BestIndieGames | Indie list reach. | YouTube route. | List placement uncertain. | Top upcoming survival games. |
| YouTube list | Get Indie Gaming | YouTube | https://www.youtube.com/@GetIndieGaming | Indie list audience. | YouTube route. | Strong trailer needed. | Most anticipated survival indies. |
| YouTube list | John Wolfe | YouTube | https://www.youtube.com/@JohnWolfe | Horror gameplay. | YouTube route. | Selective. | Dread systems, no cheap jumpscare. |
| YouTube list | Gab Smolders | YouTube | https://www.youtube.com/@GabSmolders | Horror/atmosphere. | YouTube route. | Avoid mass-key approach. | Atmospheric single-player horror demo. |
| YouTube list | CJUgames | YouTube | https://www.youtube.com/@CJUgames | Indie horror longplays. | YouTube route. | Verify official contact. | Full demo horror playthrough. |

## Steam Curator Targeting

Use Steam Curator Connect and tag discovery. Do not send raw keys to unverified curator emails.

Curator allocation and identity/activity evidence live in `Press/STEAM_CURATOR_CANDIDATE_TRACKER.csv`; this section is tag/seed navigation only. Curator replies stay curator/press provenance unless explicit separate opt-in exists.

| Curator/Tag Surface | URL | Why It Matters | Risk |
|---|---|---|---|
| Rely on Horror Steam Curator | https://store.steampowered.com/curator/6856130-Rely-on-Horror/ | Tracker-backed horror curator seed; use CUR-001 status and reverify in Steamworks before Curator Connect allocation. | Verify identity. |
| HORROR gems | https://store.steampowered.com/curator/41487727-HORROR-gems/ | Horror-focused curator. | Curator-only, no raw keys. |
| Survival Horror tag | https://store.steampowered.com/tags/en/Survival%20Horror/ | Finds active horror curators. | Manual vet required. |
| Open World Survival Craft tag | https://store.steampowered.com/tags/en/Open%20World%20Survival%20Craft/ | Survival audience. | Scam-heavy tag. |
| Underwater tag | https://store.steampowered.com/tags/en/Underwater/ | Exact setting fit. | Smaller audience. |
| Atmospheric tag | https://store.steampowered.com/tags/en/Atmospheric/ | Mood-heavy fit. | Broad/noisy. |
| Singleplayer tag | https://store.steampowered.com/tags/en/Singleplayer/ | Single-player-first alignment. | Too broad. |
| Exploration tag | https://store.steampowered.com/tags/en/Exploration/ | Exploration overlap. | Broad/noisy. |
| Base Building tag | https://store.steampowered.com/tags/en/Base%20Building/ | Habitat hook. | Needs real feature proof. |
| Crafting tag | https://store.steampowered.com/tags/en/Crafting/ | Survival crafting audience. | Crowded. |
| Sci-fi tag | https://store.steampowered.com/tags/en/Sci-fi/ | NASA-punk angle. | Broad. |
| Immersive Sim tag | https://store.steampowered.com/tags/en/Immersive%20Sim/ | Systems audience. | Tag expectation high. |

## Pitch Timing

- Before screenshots: no press pitch.
- First screenshot set: small press fit-check only.
- First 20s gameplay: small creator list and indie press.
- Demo: broad press/creator outreach.
- Next Fest: curated high-volume press/demo/list campaign.
