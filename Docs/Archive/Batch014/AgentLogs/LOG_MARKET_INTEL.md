# MARKET_INTEL Log

## 2026-05-26 Subnautica 2 V7 Competitive Ceiling Refresh

What was wrong:
- Active marketing docs still treated the 2026-05-21 SN2 V6 refresh as current.
- HECTON marketing state was still pre-screenshot: no public screenshot pack, no Steam page, no demo, no measured performance proof, and no public conversion data.
- User impression that SN2 is visually unimpressive is directionally useful but incomplete: official platform data shows SN2 is market-strong, not collapsing.
- Current asset folder scan found planning structures but no captured marketing assets to compare against SN2 yet.

What was done:
- Read authority/domain docs and relevant mandates: fake-first visuals, frame budgets, noir rendering, abyssal lighting, and fluid/VFX cost discipline.
- Read marketing strategy, control, Steam, shotlist, trailer, FAQ, press, monitoring, risk, asset metadata, and daily-loop docs.
- Verified SN2 public state on 2026-05-26 through Steam store, Steam review API, appdetails API, SteamDB, and six official Steam screenshot assets.
- Added V7 currentness and competitive ceiling to:
  - `Docs/Marketing/Monitoring/COMPETITOR_AND_SENTIMENT_MONITORING_QUERIES.md`
  - `Docs/Marketing/MARKETING_CONTROL_TOWER.md`
  - `Docs/Marketing/README.md`
  - `Docs/Marketing/PREP_DIRECTIONS_NOW.md`
  - `Docs/Marketing/Content/SCREENSHOT_AND_CLIP_SHOTLIST.md`
  - `Docs/Marketing/Operations/DAILY_AGENT_TASK_LOOP.md`
  - `Docs/Marketing/Data/MARKETING_RISK_REGISTER.md`
  - `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`
  - `Docs/Marketing/Data/SOURCE_LEDGER.md`
- Created long-memory files for this synthetic ID:
  - `Docs/Tasks/Status_MARKET_INTEL.md`
  - `Docs/AgentLogs/Rationale_MARKET_INTEL.md`

External findings:
- Steam review API, all languages: 89,716 positive / 8,629 negative / 98,345 total, `Very Positive`.
- Steam review API, English: 55,738 positive / 3,938 negative / 59,676 total, `Very Positive`.
- Steam appdetails: released 14 May 2026, 90,944 recommendations, six official screenshots, single-player, multiplayer, co-op, online co-op, cross-platform multiplayer, controller/accessibility categories, Steam Cloud, Family Sharing, Early Access.
- SteamDB is directional only, but still shows large active-player and review gravity. Official Steam sources remain primary.

Visual read:
- SN2 official screenshots are competent and readable.
- SN2 owns bright/cozy alien-ocean wonder, clean modular bases/interiors, co-op/player presence, soft rounded vehicles, and one stronger orange biome.
- SN2 does not own HECTON's target lane: industrial pressure-vessel dread, dirty machinery, corrosion, base failure, black water, route risk, or instrument-led fear.
- HECTON must not try to be a brighter reef. It must prove pressure, machinery, salvage, route decisions, and damage response in the first asset packet.

Cinematic cheats used:
- No runtime implementation was touched.
- Strategy now explicitly routes future visuals through fake-first methods: authored grime, baked silhouettes, cheap fog/dither, 1D ramps, triangle/noise waves, impostor particles, fixed-cadence instrumentation, and layered failure staging before expensive simulation.
- Gameplay truth remains separate from visual overkill; `GlobalQualityWeight` may scale fidelity/cadence/capacity, not authority or save identity.

Exact microseconds saved:
- Current pass: 0 us runtime saved and 0 us runtime cost; docs-only.
- Future savings: not claimed. Any microsecond number for later rendering must come from profiler/build/hardware/settings proof.

Validation:
- Marketing file count: 100.
- CSV parse: `CSV_PARSE_OK count=9`.
- CRM rows: 100; `DO_NOT_CONTACT=3; LOW_PRIORITY_VERIFY_LATER=52; NEEDS_ASSET=22; VERIFY_BEFORE_CONTACT=23`.
- Creator send-log fields checked: all 0.
- Asset rows: 13; expected handoff/claim headers present.
- Forbidden-pattern scan: no hits.
- Backtick path audit: `BACKTICK_PATH_AUDIT_OK`.
- SHINOBU_81 rationale-order audit: `RATIONALE_ORDER_AUDIT_NOT_APPLICABLE path_absent`.
- Build: not run. Docs-only marketing work; daily-loop validation explicitly says not to run `dotnet build` for docs-only marketing changes.

Sources:
- Steam store: `https://store.steampowered.com/app/1962700/Subnautica_2/`
- Steam all-language review API: `https://store.steampowered.com/appreviews/1962700?json=1&filter=summary&language=all&purchase_type=all&num_per_page=0`
- Steam English review API: `https://store.steampowered.com/appreviews/1962700?json=1&filter=summary&language=english&purchase_type=all&num_per_page=0`
- Steam appdetails API: `https://store.steampowered.com/api/appdetails?appids=1962700&filters=basic,categories,genres,screenshots,recommendations,release_date`
- SteamDB directional index: `https://steamdb.info/app/1962700/`

## 2026-05-26 Phase 2 Creative Counter-Position Pass

What was wrong:
- V7 currentness existed, but creative/trailer execution still needed hard rules that a capture or art agent can apply without inventing strategy.
- Without those rules, the next asset pass could drift back into bright reef beauty, clean sci-fi interiors, group-player warmth, mood-only darkness, or "Subnautica but darker" framing.

What was done:
- Sampled 100 recent English negative and 100 recent English positive SN2 reviews through official Steam appreviews API. Term hits were recorded as directional internal evidence only, not statistics for public copy.
- Updated `Monitoring/COMPETITOR_AND_SENTIMENT_MONITORING_QUERIES.md` with the V7 sample rows.
- Updated `Creative/VISUAL_IDENTITY_AND_KEY_ART_DIRECTION.md` with a V7 competitor visual ceiling table and low/middle/high/ultra scaling requirements.
- Updated `Creative/CAPSULE_TRAILER_THUMBNAIL_BRIEFS.md` with V7 capsule/thumbnail pass and kill checks.
- Updated `Content/TRAILER_SCRIPT_CAPTURE_AND_EDITING_BRIEF.md` with a V7 trailer counter-position table for the first 60 seconds.
- Added backlog row 251 and source-ledger trace for the pass.

Creative result:
- First visual proof lanes are now pressure hatch/base failure, floodlight route into black water, heavy machine under depth stress, and instrument-corrupted Seed Ship signal only if real gameplay proof exists.
- Hard rejects are now explicit: bright reef fight, co-op warmth, clean base showroom, generic diver, smooth concept surfaces, mood-only darkness, unsupported performance claims, and public competitor attack framing.

Cinematic cheats used:
- Runtime untouched.
- Future visual direction is fake-first: silhouettes, labels, warning-state materials, silt planes, 1D ramps, noise/triangle animation, light cones, impostor debris, and authored grime before expensive simulation.

Exact microseconds saved:
- Current pass: 0 us runtime saved and 0 us runtime cost; docs-only.
- Future microseconds: not claimed without profiler proof.

Validation:
- Marketing file count: 100.
- CSV parse: `CSV_PARSE_OK count=9`.
- CRM rows: 100; `DO_NOT_CONTACT=3; LOW_PRIORITY_VERIFY_LATER=52; NEEDS_ASSET=22; VERIFY_BEFORE_CONTACT=23`.
- Creator send-log fields checked: all 0.
- Asset rows: 13; expected handoff/claim headers present.
- Forbidden-pattern scan: no hits.
- Backtick path audit: `BACKTICK_PATH_AUDIT_OK`.
- SHINOBU_81 rationale-order audit: `RATIONALE_ORDER_AUDIT_NOT_APPLICABLE path_absent`.
- Active V6-currentness search: no hits.
- Build: not run; docs-only.

Sources:
- Steam recent English negatives: `https://store.steampowered.com/appreviews/1962700?json=1&language=english&purchase_type=all&filter=recent&review_type=negative&num_per_page=100`
- Steam recent English positives: `https://store.steampowered.com/appreviews/1962700?json=1&language=english&purchase_type=all&filter=recent&review_type=positive&num_per_page=100`

## 2026-05-26 Phase 3 Survey And PR-Surface Scouting Pass

What was wrong:
- Owner requested polls and PR-surface research across itch.io, wikis, DTF, and Habr.
- Existing docs had community/channel controls, but no unified survey packet, platform scout table, or explicit support notes for these specific routes.
- Without route separation, this work could become spam, fake outreach, or consent contamination.

What was done:
- Checked current source/help/rule pages for itch.io, DTF, Habr, Fandom, wiki.gg, PCGamingWiki, and IGDB.
- Added PR surface field notes and a surface scout card to `Community/COMMUNITY_TARGETS_AND_RULES.md`.
- Added platform-specific draft frames to `Community/COMMUNITY_POST_TEMPLATES.md` for itch.io page feedback, DTF RU critique/devlog, Habr technical article, and wiki readiness.
- Added gated survey packets, RU poll, and EN poll to `Feedback/PLAYER_FEEDBACK_TAXONOMY_AND_TRIAGE.md`.
- Added `Survey And Surface Scout Table` to `KPI/MARKETING_DASHBOARD_SPEC.md`.
- Added `RISK-079` and `RISK-080` to `Data/MARKETING_RISK_REGISTER.md`.
- Added backlog row 252 and source-ledger trace.

Route decisions:
- itch.io: viable later for a real playable artifact or draft page feedback; hold until metadata/platform/build truth exists.
- DTF: viable for RU no-link critique or devlog only if one real asset carries the post; store link is not the point.
- Habr: viable only for engineering content that stands without brand promotion; no player-marketing angle.
- Fandom/wiki.gg: hold until public/demo/EA content density and moderation owner exist; not a launch PR shortcut.
- PCGamingWiki/IGDB: factual/database routes only after public build/page data exists; no hype copy.

Cinematic cheats used:
- Runtime untouched.
- Survey and PR routes now require visible proof of pressure, machinery, route decision, or technical artifact before public action. This preserves fake-first visual direction and blocks expensive-performance claims without profiler proof.

Exact microseconds saved:
- Current pass: 0 us runtime saved and 0 us runtime cost; docs-only.
- Future runtime savings: not claimed. Any performance or visual-overkill claim still requires profiler/build/hardware proof.

Validation:
- Marketing file count: 100.
- CSV parse: `CSV_PARSE_OK count=9`.
- CRM rows: 100; `DO_NOT_CONTACT=3; LOW_PRIORITY_VERIFY_LATER=52; NEEDS_ASSET=22; VERIFY_BEFORE_CONTACT=23`.
- Creator send-log fields checked: all 0.
- Asset rows: 13; expected handoff/claim headers present.
- Forbidden-pattern scan: no hits.
- Backtick path audit: `BACKTICK_PATH_AUDIT_OK`.
- SHINOBU_81 rationale-order audit: `RATIONALE_ORDER_AUDIT_NOT_APPLICABLE path_absent`.
- Build: not run; docs-only marketing work and daily-loop validation forbids unnecessary `dotnet build`.

Sources:
- itch.io getting started: `https://itch.io/docs/creators/getting-started`
- itch.io quality guidelines: `https://itch.io/docs/creators/quality-guidelines`
- DTF rules: `https://dtf.ru/rules`
- DTF about: `https://dtf.ru/about`
- DTF promo: `https://dtf.ru/promo`
- Habr rules: `https://habr.com/ru/docs/help/rules/`
- Habr sandbox: `https://habr.com/ru/docs/help/sandbox/`
- Habr companies: `https://habr.com/ru/companies/`
- Fandom start/community policy: `https://community.fandom.com/wiki/Help:Start_a_new_community`, `https://www.fandom.com/community-creation-policy`
- wiki.gg: `https://www.wiki.gg/`, `https://support.wiki.gg/wiki/Creating_a_new_wiki`, `https://support.wiki.gg/wiki/Getting_Started`
- PCGamingWiki editing guide: `https://www.pcgamingwiki.com/wiki/PCGamingWiki:Editing_guide`
- IGDB content policy: `https://www.igdb.com/content-policy`

## 2026-05-26 Phase 4 Borrowed/Owned PR Surface Expansion

What was wrong:
- The Phase 3 surface layer covered the owner's first list, but the operating plan still needed clear boundaries for Reddit, Game Jolt, IndieDB/ModDB, Steam Community/News, and owned short-video channels.
- Without the boundary, a future pass could treat every surface as a broadcast slot and create spam, empty profile pages, or premature Steam/community claims.

What was done:
- Added `PR Surface Expansion V1` to `Community/COMMUNITY_TARGETS_AND_RULES.md`.
- Added `Borrowed Surface Ladder V0` to `OUTREACH_CALENDAR_AND_BATCH_PLAN.md`.
- Expanded the KPI survey/surface scout enums in `KPI/MARKETING_DASHBOARD_SPEC.md`.
- Added `RISK-081` and `RISK-082` to `Data/MARKETING_RISK_REGISTER.md`.
- Added backlog row 253 and source-ledger trace.

Route result:
- Reddit is critique/listening only by default: one subreddit, one asset, no-link, developer disclosure, same-day rules.
- Game Jolt and itch-style pages wait for real playable/package/page truth.
- IndieDB/ModDB are profile/media routes only after factual assets and maintenance owner exist.
- Steam Community/News are owner-controlled only after Steam page, moderation/support, and announcement gates pass.
- YouTube/Shorts/TikTok are owned media tests only after account custody, post gate, and first-3-second action read.

Cinematic cheats used:
- Runtime untouched.
- PR routes now require proof of visible pressure/machinery/route action before distribution. This keeps visual ambition tied to captured evidence instead of prose claims.

Exact microseconds saved:
- Current pass: 0 us runtime saved and 0 us runtime cost; docs-only.
- Future microseconds: not claimed without profiler proof.

Validation:
- Marketing file count: 100.
- CSV parse: `CSV_PARSE_OK count=9`.
- CRM rows: 100; `DO_NOT_CONTACT=3; LOW_PRIORITY_VERIFY_LATER=52; NEEDS_ASSET=22; VERIFY_BEFORE_CONTACT=23`.
- Creator send-log fields checked: all 0.
- Asset rows: 13; expected handoff/claim headers present.
- Forbidden-pattern scan: no hits.
- Backtick path audit: `BACKTICK_PATH_AUDIT_OK`.
- SHINOBU_81 rationale-order audit: `RATIONALE_ORDER_AUDIT_NOT_APPLICABLE path_absent`.
- Build: not run; docs-only marketing work and daily-loop validation forbids unnecessary `dotnet build`.

Sources:
- Reddit Rules: `https://redditinc.com/policies/reddit-rules`
- Reddit spam policy: `https://support.reddithelp.com/hc/en-us/articles/360043504051-Spam`
- Reddit community spam guidance: `https://support.reddithelp.com/hc/en-us/articles/28012014962580-How-do-I-keep-spam-out-of-my-community`
- Game Jolt add game: `https://gamejolt.com/help-docs/creators/add-game`
- Game Jolt guidelines: `https://gamejolt.com/help-docs/general/guidelines`
- Game Jolt sell games: `https://gamejolt.com/help-docs/Shop/sell-games`
- IndieDB add game: `https://www.indiedb.com/games/add`
- ModDB terms: `https://www.moddb.com/terms-of-use`
- ModDB how-to: `https://www.moddb.com/how-to`
- Steam Community: `https://partner.steamgames.com/doc/features/community?beta=1`
- Steam Events/Announcements: `https://partner.steamgames.com/doc/marketing/event_tools`
- Steam Release Options: `https://partner.steamgames.com/doc/store/types?language=english`

## 2026-05-26 Phase 5 PR Surface Expansion V3

What was wrong:
- Phase 4 still left several route families collapsed or absent: Steam-native access/discovery, creator/stream platforms, Discord, social micro-pitch channels, owned newsletter, technical publishing, secondary storefronts, crowdfunding, niche press, and high-risk forums.
- A future agent could confuse Steam Curator Connect, Playtest, Demo, and Next Fest as one "Steam push"; or confuse creator access, social posting, Discord opening, and newsletter consent as one audience route.

What was done:
- Added `PR Surface Expansion V3` to `Community/COMMUNITY_TARGETS_AND_RULES.md`.
- Added `Platform-Specific Draft Frames V3` to `Community/COMMUNITY_POST_TEMPLATES.md`.
- Expanded `KPI/MARKETING_DASHBOARD_SPEC.md` surface and route-class enums plus decision rules.
- Added `Expanded Surface Ladder V2` to `OUTREACH_CALENDAR_AND_BATCH_PLAN.md`.
- Added `RISK-088` through `RISK-093` and current top risks 55-60 to `Data/MARKETING_RISK_REGISTER.md`.
- Added backlog row 255 and source-ledger V3 trace.

Route result:
- Steam Curator Connect, Playtest, Demo, and Next Fest are separate HOLD routes with separate gates.
- Epic/GOG are secondary storefront readiness routes, not credibility props.
- Kickstarter/BackerKit are funding routes only if budget, rewards, delivery, legal/account custody, and production proof exist.
- Twitch/Kick/YouTube creator coverage requires build/access, disclosure, known issues, support owner, manual recipient fit, and key/access custody.
- Discord requires moderation/support owner and consent separation before any public invite.
- X/Bluesky/Instagram/Threads/LinkedIn/Mastodon require account custody, real media, post gate, same-day platform check, no-spam cadence, and CTA gate if linked.
- Medium/Hashnode/DEV are technical publishing only; Substack/newsletter is owned-audience only with explicit opt-in.
- Alpha Beta Gamer and GamingOnLinux are niche routes with artifact/platform-proof requirements.
- Product Hunt is kill-by-default for the game; use only for a real product/dev-tool fit.
- NeoGAF/ResetEra/Something Awful are monitor-only by default; answer existing threads only with factual developer disclosure and no CTA.

Cinematic cheats used:
- Runtime untouched.
- The marketing route work forces public proof back to inspectable assets and first-loop readability instead of prose. This preserves budget for later visual fakes: pressure labels, baked grime, light cones, silt planes, instrument desync, and route-state staging.

Exact microseconds saved:
- Current pass: 0 us runtime saved and 0 us runtime cost; docs-only.
- Future runtime savings: not claimed. No build/profiler/hardware proof exists in this pass.

Validation:
- Marketing file count: 100.
- CSV parse: `CSV_PARSE_OK count=9`.
- CRM rows: 100; `DO_NOT_CONTACT=3; LOW_PRIORITY_VERIFY_LATER=52; NEEDS_ASSET=22; VERIFY_BEFORE_CONTACT=23`.
- Creator send-log fields checked: all 0.
- Asset rows: 13; required handoff/claim headers present.
- Forbidden-pattern scan: no hits.
- Backtick path audit: `BACKTICK_PATH_AUDIT_OK`.
- SHINOBU_81 rationale-order audit: `RATIONALE_ORDER_AUDIT_NOT_APPLICABLE path_absent`.
- Build: not run; docs-only marketing work and daily-loop validation forbids unnecessary `dotnet build`.

Sources:
- Steam Curator Connect: `https://partner.steamgames.com/doc/marketing/curators`
- Steam Playtest: `https://partner.steamgames.com/doc/features/playtest?language=english`
- Steam Demos: `https://partner.steamgames.com/doc/store/application/demos?language=english`
- Steam Next Fest: `https://partner.steamgames.com/doc/marketing/upcoming_events/nextfest?language=english`
- Epic Games Store self-publishing: `https://store.epicgames.com/en-US/blog/epic-games-store-launches-self-publishing-tools-for-game-developers-and-publishers`
- GOG submit/developer docs: `https://lp.gog.com/submit-your-game/en`, `https://docs.gog.com/quick-start/`
- Kickstarter and BackerKit: `https://help.kickstarter.com/hc/en-us/articles/115005134333-Can-Kickstarter-be-used-to-fund-anything`, `https://www.backerkit.com/for-creators`
- Twitch/Kick: `https://help.twitch.tv/s/article/branded-content-policy?language=en_US`, `https://safety.twitch.tv/articles/en_US/Knowledge/Community-Guidelines`, `https://kick.com/community-guidelines`
- Discord/YouTube/social: `https://discord.com/guidelines`, `https://support.google.com/youtube/answer/2801973?hl=en`, `https://help.x.com/rules-and-policies/platform-manipulation`, `https://bsky.social/about/support/community-guidelines`, `https://www.facebook.com/help/instagram/477434105621119`
- LinkedIn/Mastodon/technical publishing/newsletter: `https://www.linkedin.com/legal/professional-community-policies`, `https://www.esafety.gov.au/key-topics/esafety-guide/mastodon`, `https://help.medium.com/hc/en-us/articles/213477928-Medium-Rules`, `https://hashnode.com/code-of-conduct`, `https://dev.to/code-of-conduct`, `https://support.substack.com/hc/en-us/articles/4404340396564-How-do-I-report-a-content-violation`
- Niche/high-risk/database: `https://www.alphabetagamer.com/contact-us/`, `https://www.gamingonlinux.com/contact-us/`, `https://help.producthunt.com/en/articles/9883485-product-hunt-featuring-guidelines`, `https://www.neogaf.com/help/terms/`, `https://www.resetera.com/threads/general-guide-to-resetera.9777/`, `https://forums.somethingawful.com/forum_rules.php`, `https://www.igdb.com/content-policy`

## 2026-05-26 Phase 6 PR Surface Expansion V4

What was wrong:
- V3 separated access, creator, social, storefront, and owned-audience routes, but still did not isolate Steam event/traffic tools, showcase nominations, and mainstream press tips.
- Without that split, future work could report Steam metrics as demand proof, submit to showcases without current gameplay, or send press "we exist" messages with no news beat.

What was done:
- Added `PR Surface Expansion V4` to `Community/COMMUNITY_TARGETS_AND_RULES.md`.
- Added `Platform-Specific Draft Frames V4` to `Community/COMMUNITY_POST_TEMPLATES.md`.
- Expanded `KPI/MARKETING_DASHBOARD_SPEC.md` surface and route-class enums plus decision rules.
- Added `Expanded Surface Ladder V3` to `OUTREACH_CALENDAR_AND_BATCH_PLAN.md`.
- Added `RISK-094` through `RISK-098` and current top risks 61-65 to `Data/MARKETING_RISK_REGISTER.md`.
- Added backlog row 256 and source-ledger V4 trace.

Route result:
- Steam Themed Sale Events, Broadcast, UTM/widget, visibility, and wishlists are measurement/event-prep only until page/build/demo gates pass.
- Future Games Show, PC Gaming Show, The MIX, Day of the Devs, Six One Indie, INDIE Live Expo, and Develop:Brighton require trailer/current gameplay, source/deadline/fee proof, presskit, and post-show owner.
- Wholesome-style routes are kill-by-default because HECTON identity is pressure/noir, not cozy/warm/cute.
- PC Gamer, GamesRadar/Future, PCGamesN, GameSpot, and Pocket Gamer require a real news beat, presskit, footage/demo, official inbox custody, and claim lint.

Cinematic cheats used:
- Runtime untouched.
- The route gates keep public attention tied to inspectable pressure/machinery/route proof instead of metric theater. Saved production effort should go into visual fakes that survive cold-read: light cones, silt planes, warning-state materials, instrument desync, pressure labels, and industrial failure staging.

Exact microseconds saved:
- Current pass: 0 us runtime saved and 0 us runtime cost; docs-only.
- Future runtime savings: not claimed. No build/profiler/hardware proof exists in this pass.

Validation:
- Marketing file count: 100.
- CSV parse: `CSV_PARSE_OK count=9`.
- CRM rows: 100; `DO_NOT_CONTACT=3; LOW_PRIORITY_VERIFY_LATER=52; NEEDS_ASSET=22; VERIFY_BEFORE_CONTACT=23`.
- Creator send-log fields checked: all 0.
- Asset rows: 13; required handoff/claim headers present.
- Forbidden-pattern scan: no hits.
- Backtick path audit: `BACKTICK_PATH_AUDIT_OK`.
- SHINOBU_81 rationale-order audit: `RATIONALE_ORDER_AUDIT_NOT_APPLICABLE path_absent`.
- Build: not run; docs-only marketing work and daily-loop validation forbids unnecessary `dotnet build`.

Sources:
- Steam themed events/upcoming events: `https://partner.steamgames.com/doc/marketing/upcoming_events/themed_sales?language=english`, `https://partner.steamgames.com/doc/marketing/upcoming_events?language=english`
- Steam broadcast/setup: `https://partner.steamgames.com/doc/store/broadcast?l=english`, `https://partner.steamgames.com/doc/store/broadcast/setting_up?l=german&language=english`
- Steam UTM/widget/visibility/wishlist: `https://partner.steamgames.com/doc/marketing/utm_analytics?language=english`, `https://partner.steamgames.com/doc/marketing/widget`, `https://partner.steamgames.com/doc/marketing/visibility?language=english`, `https://partner.steamgames.com/doc/marketing/wishlist?l=english&title=partner.steamgames.com`
- Future/PC shows: `https://www.futuregamesshow.com/`, `https://www.futuregamesshow.com/about`, `https://www.gamesradar.com/future-games-show/`, `https://www.pcgamingshow.com/`, `https://www.pcgamer.com/about-pc-gamer/`
- Indie showcases: `https://mediaindieexchange.com/showcases/`, `https://archive.mediaindieexchange.com/faq/`, `https://www.dayofthedevs.org/submit`, `https://www.sixoneindie.com/showcase`, `https://indie.live-expo.games/en/entry/`, `https://wholesomegames.com/`, `https://www.developconference.com/whats-on/indie-showcase-competition`
- Press routes: `https://www.gamesradar.com/about-gamesradar/`, `https://www.pcgamesn.com/about-us`, `https://www.gamespot.com/about/`, `https://www.pocketgamer.com/pages/contact-us/`
2026-05-26 Phase 7 PR Surface V5 Regional/Physical/B2B/Award Expansion

What was wrong:
- PR surface map still blended physical showcases, regional eligibility, B2B meetings, pitch events, and awards into generic showcase logic.
- That hid real blockers: travel, booth budget, hardware, staffing, legal/business owner, regional eligibility, language owner, fee/deadline, stale rules, and public selection-claim risk.

What was done:
- Added V5 surface map to `Docs/Marketing/Community/COMMUNITY_TARGETS_AND_RULES.md`: PAX Rising, PAX Aus Indie Showcase, BitSummit, Tokyo Game Show, Taipei Game Show, gamescom Indie Arena/Home of Indies, Calgary Indie Game Bash, ChinaJoy x Game Connection, Nordic Game, Reboot Develop monitor, Debug Indie Game Awards, BostonFIG, IndieGameBusiness Pitch LIVE, GDC Pitch, and Game Gauntlet.
- Added V5 preflight packets to `Docs/Marketing/Community/COMMUNITY_POST_TEMPLATES.md`: physical booth readiness, regional eligibility, B2B meeting/pitch, award submission, stale-rule monitor.
- Expanded KPI scout enums/rules in `Docs/Marketing/KPI/MARKETING_DASHBOARD_SPEC.md` for physical, regional, B2B, award, accelerator, stale-rule, and monitor routes.
- Added Expanded Surface Ladder V4 to `Docs/Marketing/OUTREACH_CALENDAR_AND_BATCH_PLAN.md`.
- Added risks RISK-099 through RISK-104 and top-risk items 66-71 in `Docs/Marketing/Data/MARKETING_RISK_REGISTER.md`.
- Added backlog row 257 and source-ledger V5 trace.
- Recorded V5 rationale in `Docs/AgentLogs/Rationale_MARKET_INTEL.md` and status in `Docs/Tasks/Status_MARKET_INTEL.md`.

Cinematic Cheats used:
- Docs-only. No runtime simulation, no Unity import, no asset generation.
- Marketing equivalent of fake-first: physical PR value is not assumed from event name; route is held until current proof, owner, and artifact gates exist.

Exact Microseconds saved:
- Runtime: 0 us. No code, build, Unity scene, asset import, public account, browser, outreach, event application, booth reservation, B2B meeting, award submission, public CTA, or public selection claim occurred.
- Process waste avoided: unmeasured; no profiler claim. The saved work is blocked travel/booth/submission/outreach churn before proof.

Verification:
- Marketing file count: 100.
- CSV parse: `CSV_PARSE_OK count=9`.
- CRM: `CRM rows=100`; `DO_NOT_CONTACT=3; LOW_PRIORITY_VERIFY_LATER=52; NEEDS_ASSET=22; VERIFY_BEFORE_CONTACT=23`.
- Send-log fields: all 0.
- Asset metadata: 13 rows; required headers present.
- Forbidden scan: no hits from daily-loop pattern set.
- Backtick path audit: `BACKTICK_PATH_AUDIT_OK`.
- Rationale-order audit: `RATIONALE_ORDER_AUDIT_NOT_APPLICABLE path_absent`.
- Build not run: docs-only marketing change.
## 2026-05-26 MARKET_INTEL Phase 8 - PR Surface V6

What was wrong:
- The PR map had physical/B2B/award coverage, but genre/direct/narrative/horror/art-game/Steam-festival/identity/publisher/local-station routes were not separated.
- One AdventureX source path was unsafe: `adventure-x.org` FAQ describes a different hackathon-style route, not the UK narrative convention at `adventurexpo.org`.
- Without V6 gates, the team could pitch HECTON into narrative, puzzle, horror, art, or identity surfaces with theme-only evidence and create false positioning.

What was done:
- Added V6 route notes in `Community/COMMUNITY_TARGETS_AND_RULES.md` for A MAZE. Berlin, gamescom latam BIG Festival, AdventureX UK, LudoNarraCon, Cerebral Puzzle Showcase, Women-Led/WGF monitor routes, DreadXP pitch, Indie Horror Showcase, MAGFest MIVS, and DreamHack Indie Playground.
- Corrected the AdventureX source boundary and explicitly rejected the mismatched hackathon FAQ as UK AdventureX proof.
- Added V6 preflight templates in `Community/COMMUNITY_POST_TEMPLATES.md`: genre fit, digital narrative/Steam festival, horror publisher/showcase, art/experimental award, local physical indie station, and identity eligibility.
- Expanded KPI surface/route enums and decision rules in `KPI/MARKETING_DASHBOARD_SPEC.md`.
- Added outreach ladder V5 in `OUTREACH_CALENDAR_AND_BATCH_PLAN.md`.
- Added risks RISK-105 through RISK-110 and top-risk items 72-77 in `Data/MARKETING_RISK_REGISTER.md`.
- Added backlog row 258 and source-ledger V6 trace.
- Updated `Docs/Tasks/Status_MARKET_INTEL.md` and `Docs/AgentLogs/Rationale_MARKET_INTEL.md`.

Cinematic Cheats used:
- No runtime work. The marketing rule remains visual-fake-first: future assets for these routes must prove pressure/noir through authored props, cheap haze/dither, light cones, instrument UI, fixed-cadence VFX, and readable player decisions before any expensive simulation claim.

Exact Microseconds saved:
- 0 us runtime touched. No Unity import, build, profiler, Steam event registration, publisher pitch, festival application, award submission, booth attendance, outreach, survey launch, public CTA, or account/browser action occurred.

Validation:
- Marketing files: 100.
- CSV parse: `CSV_PARSE_OK count=9`.
- CRM rows: 100; distribution `DO_NOT_CONTACT=3; LOW_PRIORITY_VERIFY_LATER=52; NEEDS_ASSET=22; VERIFY_BEFORE_CONTACT=23`.
- Send-log fields: all 0 for `outreach_batch`, `sent_date`, `contact_route_verified_for_send`, `asset_ids_sent`, `creator_utility_score`, `send_route_class`, `reply_consent_provenance`, `reply_status_after_send`.
- Asset metadata rows: 13; required headers present.
- Forbidden scan: clean.
- Backtick path audit: `BACKTICK_PATH_AUDIT_OK`.
- Rationale order audit: `RATIONALE_ORDER_AUDIT_NOT_APPLICABLE path_absent`.
- Build: not run; docs-only change.

## 2026-05-26 MARKET_INTEL Phase 9 - PR Surface V7

What was wrong:
- The PR map still allowed key hubs, PR tooling, newswires, market-data newsletters, major awards, and industry showcases to be mentally treated as normal outreach/posting routes.
- That is unsafe. These surfaces can leak keys, create grey-market risk, burn paid PR budget, publish weak releases, imply award credibility, or waste B2B meetings before the game has proof assets and owners.

What was done:
- Added V7 route notes in `Community/COMMUNITY_TARGETS_AND_RULES.md` for Keymailer, Lurkit, Terminals.io, PressEngine, Game.Press, Key Lynx, Games Press, IndieGames.Press, IGF, IndieCade, Digital Dragons, INDIGO, MDEV, XP Game Summit, Indie Cup, and GameDiscoverCo.
- Added V7 preflight templates in `Community/COMMUNITY_POST_TEMPLATES.md`: creator key hub, presskit tooling/newswire, indie press submission, major award/festival, B2B/industry showcase, and market-data/newsletter monitor.
- Expanded KPI surface/route enums and decision rules in `KPI/MARKETING_DASHBOARD_SPEC.md`.
- Added outreach ladder V6 in `OUTREACH_CALENDAR_AND_BATCH_PLAN.md`.
- Added risks RISK-111 through RISK-115 and top-risk items 78-82 in `Data/MARKETING_RISK_REGISTER.md`.
- Added backlog row 259 and source-ledger V7 trace.
- Updated `Docs/Tasks/Status_MARKET_INTEL.md` and `Docs/AgentLogs/Rationale_MARKET_INTEL.md`.

Cinematic Cheats used:
- No runtime work. Marketing equivalent of fake-first: do not buy exposure, upload keys, or publish releases to compensate for missing proof. Future assets must sell pressure/noir through authored props, cheap haze/dither, light cones, instrument UI, fixed-cadence VFX, and readable player decisions before any public access or performance language.

Exact Microseconds saved:
- 0 us runtime touched. No Unity import, build, profiler, key upload, creator access, presskit publication, paid PR tooling, press release send, award submission, B2B meeting, public claim, account/browser action, survey launch, outreach, public CTA, or runtime action occurred.

Validation:
- Marketing files: 100.
- CSV parse: `CSV_PARSE_OK count=9`.
- CRM rows: 100; distribution `DO_NOT_CONTACT=3; LOW_PRIORITY_VERIFY_LATER=52; NEEDS_ASSET=22; VERIFY_BEFORE_CONTACT=23`.
- Send-log fields: all 0 for `outreach_batch`, `sent_date`, `contact_route_verified_for_send`, `asset_ids_sent`, `creator_utility_score`, `send_route_class`, `reply_consent_provenance`, `reply_status_after_send`.
- Asset metadata rows: 13; required headers present.
- New forbidden scan: clean.
- Dangerous backtick path audit: OK.
- Build: not run; docs-only change.

## 2026-05-26 MARKET_INTEL Phase 10-11 - PR Surface V8/V9

What was wrong:
- The route map still left platform-holder, handheld/cloud, publisher, fund, grant, regional storefront, RU media/community, reseller, database/artwork, and pitch-resource surfaces too easy to mentally treat as generic PR.
- That is false readiness. These routes can create platform availability lies, devkit/cert debt, cloud-as-low-spec fraud, publisher-fit drift, grant-as-runway fantasy, paid/editorial confusion, regional legal/support debt, reseller key/discount risk, and promotional database pollution.

What was done:
- Added V8 route notes in `Community/COMMUNITY_TARGETS_AND_RULES.md` for ID@Xbox/Microsoft Game Dev, PlayStation Partners, Nintendo Developer Portal, Epic Games Store, Steam Deck, GeForce NOW, Amazon Luna, Kowloon Nights, Epic MegaGrants, Outersloth, Devolver, Fellow Traveller, No More Robots, Hooded Horse, and Team17.
- Added V9 route notes for VK Play, WeGame, TapTap, Green Man Gaming, Pikabu, vc.ru, PlayGround.ru, Kanobu, StopGame, Secret Mode, tinyBuild pitch resources, MobyGames, Giant Bomb, RAWG, and SteamGridDB.
- Added V8/V9 preflight templates in `Community/COMMUNITY_POST_TEMPLATES.md`: platform holder, secondary storefront, handheld, cloud, publisher/fund, grant, regional storefront, RU media/community, reseller/distribution, database/artwork catalog, and publisher pitch resource packets.
- Expanded KPI surface/route enums and decision rules in `KPI/MARKETING_DASHBOARD_SPEC.md`.
- Added outreach ladders V7/V8 in `OUTREACH_CALENDAR_AND_BATCH_PLAN.md`.
- Added risks RISK-116 through RISK-127 and current top-risk items 83-94 in `Data/MARKETING_RISK_REGISTER.md`.
- Added backlog rows 260-261 and source-ledger V8/V9 traces.
- Updated `Docs/Tasks/Status_MARKET_INTEL.md` and `Docs/AgentLogs/Rationale_MARKET_INTEL.md`.

Cinematic Cheats used:
- No runtime work. Marketing equivalent of fake-first: do not buy credibility from logos, cloud, regions, publishers, paid media, stores, databases, or pitch forms. First proof still has to be HECTON-native: pressure, machinery, black water, route risk, salvage failure, and readable player decision.

Exact Microseconds saved:
- 0 us runtime touched. No Unity import, build, profiler, regional publication, store submission, SDK integration, platform application, devkit request, cloud opt-in, paid media, editorial pitch, publisher/fund/grant pitch, database edit, artwork upload, account/browser action, survey launch, outreach, public CTA, or runtime action occurred.

Validation:
- Marketing files: 100.
- CSV parse: `CSV_PARSE_OK count=9`.
- CRM rows: 100; distribution `DO_NOT_CONTACT=3; LOW_PRIORITY_VERIFY_LATER=52; NEEDS_ASSET=22; VERIFY_BEFORE_CONTACT=23`.
- Send-log fields: all 0 for `outreach_batch`, `sent_date`, `contact_route_verified_for_send`, `asset_ids_sent`, `creator_utility_score`, `send_route_class`, `reply_consent_provenance`, `reply_status_after_send`.
- Asset metadata rows: 13; required headers present.
- Daily-loop forbidden scan: clean.
- Platform-claim scan: clean after narrowing generic `approved by` to platform-context wording.
- Backtick path audit: `BACKTICK_PATH_AUDIT_OK`.
- Rationale order audit: `RATIONALE_ORDER_AUDIT_NOT_APPLICABLE path_absent`.
- Build: not run; docs-only change.
