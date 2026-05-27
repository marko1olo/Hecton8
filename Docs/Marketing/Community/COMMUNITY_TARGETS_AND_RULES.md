# Community Targets And Posting Rules

## Authority Boundary

Static documentation only. Current source, active architecture contracts, fresh proof artifacts, and official platform rules override dated claims in this file. No runtime, profiler, memory, render, platform, public-page, or ship-readiness proof is implied by this file alone.

Status: target map / verify live rules before posting

## Prime Rule

Community posting is for critique and signal, not drive-by promotion.

Every subreddit/forum/Discord must have its rules checked immediately before posting. Rules change. Mods do not care that a strategy doc said something was acceptable.

Agency-proof boundary: community posts can ask whether a player decision reads, but they do not advance first-public, Steam, paid, creator, press, or Discord-open gates unless the asset metadata row has non-pending `viewer_named_decision`, valid non-held `capture_verdict`, and the resulting row preserves the viewer-named decision in `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`.

## Post Types That Are Usually Safer

- critique request;
- WIP screenshot feedback;
- systems design question;
- demo feedback after demo exists;
- postmortem/technical process;
- Steam page feedback in developer communities;
- no-link image post where rules forbid promotion.

## Post Types That Usually Fail

- "Wishlist our game";
- "Subnautica killer";
- unsolicited Steam link;
- AI-looking promo copy;
- posting the same image everywhere;
- arguing with critics;
- hiding that you are the developer;
- co-op bait.

## Target Buckets

### Survival / Crafting Players

Use when:

- screenshot shows base, salvage, resource loop, tool use.

Ask:

- Does the survival risk look fair?
- Does the base look functional?
- What would make pressure failure readable?
- What decision do you think the player has here?

Avoid:

- pure beauty shots;
- heavy lore;
- Steam link first.
- claiming gameplay/pressure/route-risk proof before metadata `viewer_named_decision`/`capture_verdict` and AB-009/KPI decision-read fields exist.

### Horror / Atmosphere Players

Use when:

- clip shows sonar, darkness, silhouette, pressure, audio.

Ask:

- Is it dread or just darkness?
- Does the threat read before the reveal?
- What pressure decision would the player make next?
- Does the player have a readable choice before the danger hits?
- Did the sound/visual cue work?

Avoid:

- calling it horror if gameplay cannot support it;
- jump-scare framing.

### Indie Dev / GameDev Communities

Use when:

- asking for store page, capsule, trailer, retention, UX, or production feedback.

Ask:

- Which Steam capsule reads better?
- Does the short description communicate the hook?
- What is unclear in the first 15 seconds?

Avoid:

- pretending to be a player;
- hiding commercial intent.

### Underwater / Thalassophobia Adjacent

Use when:

- asset is visually strong and not spam.

Ask:

- Does this sell depth?
- What makes water scary here?
- Is the scale readable?
- Is the route or retreat choice readable?

Avoid:

- clinical/trigger-bait language;
- insensitive fear exploitation.

### Simulation / Engineering

Use when:

- machine/system/base failure is visible.

Ask:

- Does the machinery feel like it could work?
- Does the failure state read as mechanical?
- What feedback would make the system more understandable?
- Which lever/repair/retreat decision is visible?

Avoid:

- fake realism claims;
- pretending every system is physically simulated.

## Potential Communities To Verify

Do not post without checking rules.

| Bucket | Candidate | Notes |
|---|---|---|
| survival | r/SurvivalGaming | Verify self-promo rules. |
| survival | r/BaseBuildingGames | Useful if base screenshots are real. |
| survival | r/craftinggames | Smaller fit; verify activity. |
| underwater | r/subnautica | High sensitivity; do not advertise; use only if rules and tone allow critique. |
| underwater | r/Subnautica_Below_Zero | Same warning. |
| horror | r/HorrorGaming | Use dread clips, not store links. |
| horror | r/IndieHorror | Demo/clip feedback if allowed. |
| indie | r/IndieDev | Development/process feedback. |
| indie | r/gamedev | Store/capsule/process only; strict self-promo risk. |
| indie | r/DestroyMyGame | Useful for brutal critique if asset is ready. |
| Unity/dev | r/Unity3D | Technical visuals/process, not player marketing. |
| Steam | Steam discussions/community | Only through official page when ready. |
| Discord | survival game servers | Only explicit self-promo or dev channels. |
| Discord | indie dev servers | Feedback channels only. |

## 2026-05-26 PR Surface Field Notes V0

Evidence boundary: platform rules and official/help surfaces checked on 2026-05-26. Recheck same-day before posting, creating pages, collecting survey answers, or linking to any destination.

These surfaces are not one funnel. Treat each as a different route with its own intent, permission, and proof asset.

| Surface | Use | Entry asset | Default route | Hard blockers | Source |
|---|---|---|---|---|---|
| itch.io | Later demo/prototype page, devlog, build-hosting, limited keys, or no-link page feedback before Steam. | Real playable build, web build, or downloadable prototype with exact platform truth and screenshots. | `platform_page_feedback` until public CTA gates exist. | No build, misleading platform flags, tag/classification spam, placeholder page, fake price/demo promise, unsupported performance claims. | `https://itch.io/docs/creators/getting-started`, `https://itch.io/docs/creators/quality-guidelines` |
| DTF | RU long-form devlog, screenshot critique, postmortem, or "how we solve pressure/machinery readability" article. | Real screenshot/clip or technical production artifact. | `no_link_feedback` first; link only after destination-specific CTA gate and same-day DTF rule check. | Spam, aggressive external links, commercial-service framing, giveaway bait, toxic competitor framing, profanity-heavy official tone. | `https://dtf.ru/rules`, `https://dtf.ru/about`, `https://dtf.ru/promo` |
| Habr | Technical article only: fake-first underwater visuals, deterministic systems, tooling, profiling, data pipeline, Unity production constraints. Not player marketing. | Measured technical artifact, code/diagram, profiler proof, or production case. | `technical_article_feedback` / no wishlist CTA. | Sales copy, game PR outside allowed routes, brand/logo-heavy post, unsupported performance claims, shallow dev diary. | `https://habr.com/ru/docs/help/rules/`, `https://habr.com/ru/docs/help/sandbox/`, `https://habr.com/ru/companies/` |
| Fandom | Community wiki only after enough game content and community need exist. Not launch PR. | Public build/demo/EA content, stable lore/mechanics terms, screenshot-safe references. | `wiki_readiness_monitor` until community exists. | Owner-only promotional wiki, duplicate topic, proprietary-only copy, no public editable plan, no moderation owner. | `https://community.fandom.com/wiki/Help:Start_a_new_community`, `https://www.fandom.com/community-creation-policy` |
| wiki.gg | Potential official/catered wiki after demo/EA if there is enough mechanics/lore to document and a maintainer group. | Stable guide/lore/mechanic data, icons/screenshots allowed for wiki use, owner/mod list. | `wiki_application_hold` until content density exists. | No community/helper team, no public documentation volume, duplicate/fork content risk, using wiki as an ad page. | `https://www.wiki.gg/`, `https://support.wiki.gg/wiki/Creating_a_new_wiki`, `https://support.wiki.gg/wiki/Getting_Started` |
| PCGamingWiki | Post-release or public-demo technical compatibility data: save path, settings, FOV, ultrawide, controller, cloud saves, fixes. | Public PC build and verified technical data. | `technical_database_update` after build exists. | Pre-release hype, unverified settings, promotional prose, missing reproducible data. | `https://www.pcgamingwiki.com/wiki/PCGamingWiki:Editing_guide` |
| IGDB / databases | Basic factual listing after official assets/page exist. | Title, release window/state, genres, platforms, screenshots, official URL. | `database_listing` after public identity exists. | Marketing claims, fake platforms, unverified release date, duplicate or unofficial entry confusion. | `https://www.igdb.com/content-policy` |

### Surface Priority

1. `SURVEY_PREP`: build cold-read and surface-fit surveys before any public route.
2. `ITCH_PAGE_READINESS`: prepare itch page only when a real playable artifact or public prototype exists.
3. `DTF_DEVLOG_READY`: write a RU devlog only when one real asset can carry the article without a store link.
4. `HABR_TECH_READY`: write Habr only when there is a defensible technical artifact; otherwise hold.
5. `WIKI_HOLD`: wikis wait for demo/EA content density and moderation owner.
6. `DATABASE_HOLD`: IGDB/PCGamingWiki wait for public factual state and build data.

### Surface Scout Card

```text
Surface:
Same-day rule/source URL:
Intended route class:
Post/page/account owner:
Required asset or artifact:
Allowed link state: no-link / raw URL / UTM / none
Consent/provenance field:
Main question:
Hard blockers:
Stop condition:
Decision: HOLD / PREP / READY_FOR_HUMAN_REVIEW / KILL
```

## 2026-05-26 PR Surface Expansion V1

Evidence boundary: additional source/help/rule surfaces checked on 2026-05-26. This table extends the scout map; it does not authorize account creation, page creation, posting, outreach, or public links.

| Surface | Use | Entry asset | Default route | Hard blockers | Source |
|---|---|---|---|---|---|
| Reddit | Subreddit-specific critique, store-page/capsule feedback, player-language listening, narrow dev questions. | One real screenshot/clip/capsule/page mock and a same-day subreddit rule check. | `subreddit_no_link_critique` by default. | Repeated cross-posting, unsolicited promotion, hidden dev status, tracking links, DMs, sockpuppets, subreddit rule mismatch, post history that is only self-promo. | `https://redditinc.com/policies/reddit-rules`, `https://support.reddithelp.com/hc/en-us/articles/360043504051-Spam`, `https://support.reddithelp.com/hc/en-us/articles/28012014962580-How-do-I-keep-spam-out-of-my-community` |
| Game Jolt | Secondary game page/devlog after playable artifact, web build, or downloadable prototype exists. | Real package/build, thumbnail, screenshots, honest tags, platform truth. | `game_profile_page_hold` until build/page truth exists. | No playable artifact, fake platform tag, misleading build/package state, community-rule mismatch, treating followers as newsletter/tester consent. | `https://gamejolt.com/help-docs/creators/add-game`, `https://gamejolt.com/help-docs/general/guidelines`, `https://gamejolt.com/help-docs/Shop/sell-games` |
| IndieDB / ModDB | Developer/game profile, media mirror, article/devlog, long-tail discovery after real media exists. | Real screenshots/video, factual status, owned-rights media, profile owner. | `database_profile_page_hold` until assets and status are factual. | Advertising-only copy where terms disallow it, third-party/IP-unclear media, inaccurate release/build info, file upload without support owner, abandoned profile. | `https://www.indiedb.com/games/add`, `https://www.moddb.com/terms-of-use`, `https://www.moddb.com/how-to` |
| Steam Community / Discussions | Official community hub only after Steam Coming Soon/page gates. Support, FAQ, issue routing, community feedback. | Public Steam page, moderation owner, support/FAQ routes, known issue boundaries. | `steam_owner_community_hold` until Steam page/support gates allow. | Using Steam forum as pre-page PR, no moderation owner, no support route, unproved demo/build/access claim, hostile competitor framing. | `https://partner.steamgames.com/doc/features/community?beta=1`, `https://partner.steamgames.com/doc/store/types?language=english` |
| Steam Events / News | Owner-controlled announcements after Steam page/live event gates. | Approved Steam page/event asset, exact announcement permission gate, no unsupported feature/performance claims. | `steam_owner_announcement_hold` until `steam_announcement_permission_gate` allows. | Using Steam event for minor noise, cross-promotion misclassification, page not live, event lacks asset proof, public CTA gate held. | `https://partner.steamgames.com/doc/marketing/event_tools` |
| YouTube / Shorts | Owned trailer/devlog/clip hosting and creator-facing proof archive. | 16:9 trailer/clip, vertical cut only if the first 3 seconds read, exact asset ID. | `owned_media_clip_test` after account custody and post gate. | Account custody held, clip needs caption to explain action, unsupported performance/build claim, duplicate spam across Shorts/TikTok/Reels. | Existing social playbook plus platform source recheck before upload. |
| TikTok / Reels / Shorts syndication | Vertical retention smoke only after one real gameplay beat reads without caption. | 9:16 clip with pressure/machine/route decision visible in first 3 seconds. | `owned_media_clip_test` after account custody and post gate. | Mood-only water, no player verb, fake mobile gameplay expectation, untracked repost storm, comments mostly read AI/concept/clone. | Existing social/ad playbooks plus platform source recheck before upload. |

### Borrowed Surface Rule

Borrowed communities are not distribution slots. They are only useful when the post gives that community something to judge without leaving the page.

`READY_FOR_HUMAN_REVIEW` requires:

- same-day rule/source URL;
- disclosed developer/official status where relevant;
- exact asset/artifact ID;
- one question the media can answer;
- route class and consent provenance;
- no public CTA unless the exact destination gate and `public_cta_permission_gate` pass;
- stop condition if shill/spam/clone/AI-concept accusations dominate.

### Surface Order After Real Assets

1. `SURVEY_ASSET_5SEC`: blind readers before any community post.
2. `REDDIT_SINGLE_CRITIQUE`: one subreddit only, no-link, if rules allow.
3. `DTF_RU_CRITIQUE`: one RU devlog/critique only if asset carries the post.
4. `OWNED_SHORT_CLIP`: YouTube/Shorts/TikTok only after account custody and post gate.
5. `ITCH_OR_GAMEJOLT_PAGE_FEEDBACK`: only after a real playable artifact or page mock exists.
6. `STEAM_PAGE_AND_NEWS`: only after Steam page/event gates.
7. `INDIEDB_MODDB_PROFILE`: only after factual media/status and maintenance owner.
8. `WIKI_DATABASE`: only after public content/build facts exist.

## 2026-05-26 PR Surface Expansion V2

Evidence boundary: additional devlog, RU/CIS, PR-tool, database, and showcase surfaces checked on 2026-05-26. This expands scouting only. It does not authorize posting, page creation, account creation, key uploads, paid services, submissions, surveys, or public links.

### Devlog And Forum Surfaces

| Surface | Use | Entry asset | Default route | Hard blockers | Source |
|---|---|---|---|---|---|
| TIGSource forums | Long-form devlog, WIP thread, blunt indie dev critique. | Real screenshots or playable prototype; one recurring devlog thread, not drive-by promotion. | `devlog_forum_thread_hold` until same-day forum access/rules check. | No account history, one-off ad post, asset needs explanation, no reply owner, forum rules unavailable or inaccessible. | `https://forums.tigsource.com/index.php?board=53.0` |
| GameDev.net | Project page, Indie Showcase, technical/dev process article. | Project profile with factual media, build state, and developer-owned page. | `devlog_forum_thread_hold` / `project_showcase_hold`. | Empty project page, generic Steam link, no technical/community value, unsupported build/demo claim. | `https://www.gamedev.net/`, `https://www.gamedev.net/projects/about/` |
| GameDev.ru | RU technical/dev critique, rendering/system post, possible WIP thread. | Technical artifact or real WIP capture with specific question. | `ru_dev_forum_critique_hold`. | Pure PR, external resource shilling, off-topic flood, profanity/politics/insults, no Russian-language owner for replies. | `https://gamedev.ru/site/forum/?id=175081` |
| StopGame blogs/community | RU player-facing devlog only after a strong real asset or demo beat exists. | Real media and a story useful to readers without leaving the page. | `ru_media_blog_hold`. | Store-link-first copy, weak asset, no discussion value, no moderation owner, competitor-bait headline. | `https://stopgame.ru/rules` |
| Pikabu | RU broad community post only if it can stand as a story/process post, not an ad. | Real image/clip plus narrowly framed question or production story. | `ru_broad_community_hold`. | Commercial/ad read, duplicate posting, external-link aggression, no community fit, no consent/provenance row. | `https://pikabu.ru/information/rules`, `https://pikabu.ru/information/adrules` |
| vc.ru | RU business/dev process article, not player acquisition. | Production case, market/process analysis, or platform/tooling story with factual proof. | `ru_business_article_hold`. | Commercial account restrictions ignored, sales CTA, repeated copies, misleading headline, weak formatting. | `https://vc.ru/rules`, `https://vc.ru/support`, `https://vc.ru/booster-rules` |
| VK Play developer/media | Future RU distribution/media route after build and regional strategy exist. | Build package, store-card materials, owner account, regional copy review. | `regional_storefront_hold`. | No RU build/support owner, no VK Play moderation readiness, page implies release/demo before artifact, account custody absent. | `https://developers.vkplay.ru/`, `https://documentation.vkplay.ru/f2p_vkp/f2p_dashboard_overview_vkp`, `https://documentation.vkplay.ru/f2p_vkp/f2p_publish_vkp` |

### PR Tools And Key Distribution Surfaces

| Surface | Use | Entry asset | Default route | Hard blockers | Source |
|---|---|---|---|---|---|
| Games Press | Press release and asset distribution after presskit and public facts exist. | Presskit, factsheet, screenshots, trailer, contact custody, release beat. | `press_distribution_hold`. | Press release gate held, no real assets, no official inbox, no public CTA custody, no release/news beat. | `https://www.gamespress.com/en-US/`, `https://www.gamespress.com/tr/How-to-Submit-News` |
| Game.Press | Review/press/creator request hub after demo/preview route exists. | Stable build/access policy, presskit, key/access log, disclosure. | `press_distribution_hold`. | No preview build, no key/access gate, no known-issues copy, no inbox custody, no recipient filtering. | `https://game.press/` |
| PressEngine | PR tooling candidate for tracked campaigns, not a proof substitute. | Real campaign beat, presskit, recipient rules, budget approval. | `pr_tooling_hold`. | Using tool to compensate for weak assets, paid route without spend gate, no tracking owner, no sender custody. | `https://pressengine.net/` |
| Keymailer | Creator/key request surface after build, key policy, and creator-filter rules exist. | Steam keys or access route, creator selection criteria, revocation plan, known issues. | `key_distribution_hold`. | Uploading keys before stable build, auto-approving requests, no grey-market risk plan, no `private_access_permission_gate`. | `https://www.keymailer.co/allfaq`, `https://keymailer.co/creators` |
| Lurkit | Creator campaign/program route after demo or preview build exists. | Campaign page, build/access truth, creator criteria, tracking and disclosure. | `key_distribution_hold`. | Pre-launch campaign without build proof, misleading perks, creator program as fake community, no access log. | `https://support.lurkit.com/what-is-a-pre-launch-campaign`, `https://support.lurkit.com/migration/creator-program-setup` |
| Terminals.io | Presskit/game page discovery route for media, creators, and enthusiasts. | Presskit, news, videos, screenshots, optional review/access route. | `press_distribution_hold`. | No presskit, no footage, no public facts, no owner to answer requests. | `https://www.terminals.io/support/faq` |
| Woovit / legacy key hubs | Monitor only until current platform health and developer terms are rechecked. | None for now. | `monitor_only`. | Any key upload or access promise before same-day service verification and access gates. | `https://woovit.com/creator/woovit?offset=20` |

### Showcase, Festival, Hashtag, And Database Surfaces

| Surface | Use | Entry asset | Default route | Hard blockers | Source |
|---|---|---|---|---|---|
| #PitchYaGame | Twice-yearly social pitch event. | Best short pitch, 1 video/gif, 1 link only if CTA gate allows, platform-tailored copy. | `hashtag_event_hold`. | Pitching before assets, more than one pitch per game/platform, NDA risk, spam, weak media, no account custody. | `https://pitchyagame.com/`, `https://pitchyagame.com/how-to-pitch/` |
| Indie Cup | Award/showcase route for current gameplay footage/build. | At least 5 minutes of current gameplay footage for Round I and playable build for Round II. | `showcase_submission_hold`. | No current footage, no playable build, buggy/unreviewable build, false submission info. | `https://indiecup.net/rules/` |
| IndieCade | Festival submission if documentation and build/video can represent the project. | Playable build or strong documentation package, submission fee approval. | `showcase_submission_hold`. | No build/documentation, no fee approval, no owner for submission materials. | `https://www.indiecade.com/submissions/` |
| DevGAMM Awards / Showcase | Indie/small-studio award or showcase route after playable build exists. | Playable build, trailer/media, region/event fit. | `showcase_submission_hold`. | No playable build, not enough polish, no submission owner, event route mismatch. | `https://devgamm.com/awards2026/`, `https://devgamm.com/make-games-not-war/devgamm-showcase/` |
| gamescom Indie Area / Indie Arena / gamescom award | High-cost/high-effort route only after demo/booth plan exists. | Playable demo, booth/support budget, travel/ops owner, current submission window. | `showcase_submission_hold`. | No playable booth demo, no budget, no onsite owner, missed deadline, submission fee not approved. | `https://www.gamescom.global/en/program/gamescom-award`, `https://letscodegames.com/en/submission/submission` |
| DreadXP Indie Horror Showcase | Horror-adjacent showcase monitor route. | Horror/dread clip with actual gameplay and playable context. | `horror_showcase_monitor`. | HECTON asset reads survival systems more than horror, no demo/clip, no current submission route. | `https://www.dreadxp.com/`, `https://www.dreadcentral.com/news/540789/dreadxp-is-bringing-the-screams-with-the-3rd-annual-indie-horror-showcase/` |
| Horror Game Awards | Monitor only; official site says there is no formal submission process. | Public horror-game footprint after release/year eligibility. | `awards_monitor_only`. | Treating it as a submit route, vote begging, fake award language. | `https://www.thehorrorgameawards.com/` |
| MobyGames | Factual database after official public page/build/credits exist. | Official title, release/platform facts, credits/source proof. | `database_listing`. | Pre-proof hype, unverifiable credits, false release date/platform. | `https://www.mobygames.com/info/standards/` |
| RAWG | Discovery/database metadata after official page and store links exist. | Store links, official website, screenshots/video, genres/platforms. | `database_listing`. | Treating API/discovery data as PR proof, unverified metadata, no official source. | `https://rawg.io/`, `https://rawg.io/apidocs` |
| Giant Bomb wiki | Factual wiki/database entry after public identity exists. | Official page, release facts, media, sourced description. | `database_listing`. | Promotional copy, duplicate/unverified entry, no source. | `https://www.giantbomb.com/game/create/`, `https://www.giantbomb.com/forums/delete-combine-requests-34/wiki-rules-faq-1466069/` |

### V2 Priority

1. `DEVLOG_FORUM_PREP`: prepare TIGSource/GameDev.net/GameDev.ru only as devlog/critique routes with real media.
2. `RU_SURFACE_HOLD`: StopGame/Pikabu/vc.ru/VK Play wait for Russian owner, real asset, and route fit.
3. `PR_TOOL_HOLD`: Games Press/Game.Press/PressEngine/Keymailer/Lurkit/Terminals wait for presskit, build/access, inbox, and permission gates.
4. `SHOWCASE_MONITOR`: Indie Cup/IndieCade/DevGAMM/gamescom/DreadXP are deadline monitors until playable footage/build and submission gate exist.
5. `DATABASE_FACTS_ONLY`: MobyGames/RAWG/Giant Bomb are factual maintenance, not hype.

## 2026-05-26 PR Surface Expansion V3

Evidence boundary: additional Steam-native, storefront, creator, social, newsletter, professional, and high-risk forum surfaces checked on 2026-05-26. This is scouting only. It does not authorize account creation, page publication, Discord opening, posting, key/access distribution, paid promotion, crowdfunding, or public CTA links.

### Steam-Native And Storefront Discovery

| Surface | Use | Entry asset | Default route | Hard blockers | Source |
|---|---|---|---|---|---|
| Steam Curator Connect | Review-copy route for selected Steam curators after page/build/access gates. Prefer Steam-native copy delivery over raw key emails. | Steam app/page, current playable build, curator-fit list, known issues, access log, disclosure copy. | `curator_connect_hold`. | No Steam app/page, no playable build, raw-key workaround, auto-approving curators, no `private_access_permission_gate`, no coverage expectation boundary. | `https://partner.steamgames.com/doc/marketing/curators` |
| Steam Playtest | Controlled playtest data and launch readiness, not awareness bait. | Steam page, playable branch, tester scope, support owner, feedback form, known issues, revoke/deactivate plan. | `steam_playtest_hold`. | No stable build, no support route, no tester segmentation, no crash/known-issues handling, treating signup count as marketing KPI. | `https://partner.steamgames.com/doc/features/playtest?language=english` |
| Steam Demo / Next Fest | Public demo discovery after the first-loop route is playable and support-ready. | Demo app, Steam page, capsule/trailer, demo length, known issues, support/moderation owner, event deadline proof. | `next_fest_demo_hold`. | Demo cannot start cleanly, first 10 minutes unreadable, no support owner, no event registration proof, event beat used before `demo_public_access_permission_gate`. | `https://partner.steamgames.com/doc/store/application/demos?language=english`, `https://partner.steamgames.com/doc/marketing/upcoming_events/nextfest?language=english` |
| Epic Games Store | Secondary storefront candidate after Steam/page truth and regional/support strategy exist. | Developer portal custody, build/package, store assets, rating/state proof, support owner. | `secondary_storefront_hold`. | Using EGS as early credibility without build, missing account custody, incomplete ratings/compliance, page drift from Steam truth. | `https://store.epicgames.com/en-US/blog/epic-games-store-launches-self-publishing-tools-for-game-developers-and-publishers` |
| GOG | DRM-free/curated storefront candidate after build maturity and store review fit. | GOG submission or developer portal route, build package, public facts, support owner, DRM-free position. | `secondary_storefront_hold`. | No accepted/reviewed route, no build, no support owner, store page implies release/demo before artifact. | `https://lp.gog.com/submit-your-game/en`, `https://docs.gog.com/quick-start/` |
| Kickstarter / BackerKit | Funding/community route, not PR filler. Use only if campaign delivery, rewards, budget, and production evidence exist. | Campaign plan, reward/delivery proof, budget, legal/account custody, playable/visual proof, fulfillment owner. | `crowdfunding_hold`. | Using crowdfunding to fake demand, no delivery path, reward promises exceed build truth, no financial/legal owner. | `https://help.kickstarter.com/hc/en-us/articles/115005134333-Can-Kickstarter-be-used-to-fund-anything`, `https://www.backerkit.com/for-creators` |

### Creator, Stream, And Community Surfaces

| Surface | Use | Entry asset | Default route | Hard blockers | Source |
|---|---|---|---|---|---|
| Twitch creators | Organic or paid live coverage after build/access and disclosure rules exist. | Build/access route, presskit, known issues, disclosure wording, streamer fit, moderation/support owner. | `creator_stream_coverage_hold`. | No stable build, no disclosure, no category/age/content review, no support owner, payment without `paid_creator_permission_gate`. | `https://help.twitch.tv/s/article/branded-content-policy?language=en_US`, `https://safety.twitch.tv/articles/en_US/Knowledge/Community-Guidelines` |
| Kick creators | Secondary live coverage route with plain paid-promotion disclosure if value is exchanged. | Same as Twitch plus platform-fit review. | `creator_stream_coverage_hold`. | Platform fit unclear, disclosure vague, no moderation/support owner, outreach done as spam. | `https://kick.com/community-guidelines`, `https://kick.com/advertising-policy` |
| Discord official server | Owned support/community only after moderation, channel, support, and invite gates. | Server plan, moderator owner, rules, support escalation, public invite gate, no-leak private access policy. | `discord_open_hold`. | No moderation owner, no support route, no public invite gate, using Discord as default tester/newsletter consent. | `https://discord.com/guidelines`, `https://support.discord.com/hc/en-us/articles/4409308485271-Discovery-Guidelines` |
| External Discord servers | Critique/listening route only where the server explicitly allows it. | Real asset, exact server/channel rule, disclosed developer status, one feedback question. | `external_discord_critique_hold`. | DMs, invite drops, store links, repeated posts, no rule proof, no permission from moderators when required. | `https://discord.com/guidelines` |
| YouTube creator coverage | Video/short creator route after build/access or public trailer proof. | Presskit, footage, build/access, known issues, disclosure, no misleading thumbnail/title. | `creator_video_coverage_hold`. | No build/footage, deceptive title/thumbnail, mass comment/link spam, unsupported performance or feature claims. | `https://support.google.com/youtube/answer/2801973?hl=en` |

### Social, Newsletter, And Technical Publishing Surfaces

| Surface | Use | Entry asset | Default route | Hard blockers | Source |
|---|---|---|---|---|---|
| X | Short proof beats, event hashtags, direct creator/news discovery only after account custody. | One real clip/image, account custody, post gate, event/source check, CTA gate if linked. | `social_micro_pitch_hold`. | Platform manipulation, repeated identical posts, auto-DMs, fake accounts, engagement farming, unproved link/CTA. | `https://help.x.com/rules-and-policies/platform-manipulation`, `https://docs.x.com/developer-guidelines` |
| Bluesky | Short proof beats and dev/community discovery with spam/impersonation caution. | Real clip/image, verified handle/domain plan if possible, post gate, no duplicate burst. | `social_micro_pitch_hold`. | Spam-like repeat posting, fake official identity, link-only posts, no account custody. | `https://bsky.social/about/support/community-guidelines` |
| Instagram / Reels / Threads | Visual-first short clips after account custody and 3-second readability. Paid creator work uses disclosure tooling. | 9:16 clip, caption, asset ID, post gate, partnership/disclosure route if paid. | `social_short_video_hold`. | Repetitive comments, artificial engagement, repeated commercial contact, fake reviews/ratings, misleading account identity. | `https://www.facebook.com/help/instagram/477434105621119`, `https://www.facebook.com/help/instagram/616901995832907?locale=en_GB` |
| LinkedIn | Studio/business/technical credibility, partner hiring, production case notes. Not player acquisition. | Technical/business artifact, company page custody, no sales pitch, proof of claim. | `professional_business_post_hold`. | Generic game PR, job/partnership bait without owner, inflated studio claims, group self-promo mismatch. | `https://www.linkedin.com/legal/professional-community-policies`, `https://www.linkedin.com/help/linkedin/answer/a569220` |
| Mastodon | Instance-specific devlog/listening. Treat every server as its own ruleset. | Server rules, account identity, real artifact, no link burst. | `federated_social_hold`. | Assuming global Mastodon rules, commercial-only account on a no-ad instance, duplicate cross-posting, no instance owner check. | `https://www.esafety.gov.au/key-topics/esafety-guide/mastodon` |
| Medium / Hashnode / DEV | Technical or production-post route. Use only if the article stands without selling the game. | Real technical artifact, measurements if claimed, source code/images rights, no SEO-spam framing. | `technical_article_hold`. | Clickbait, duplicate/cross-post spam, AI/SEO slop, article exists only to drive traffic, no measured proof. | `https://help.medium.com/hc/en-us/articles/213477928-Medium-Rules`, `https://hashnode.com/code-of-conduct`, `https://dev.to/code-of-conduct` |
| Substack / newsletter | Owned audience only after explicit opt-in. | Signup source, consent copy, unsubscribe route, send owner, frequency policy. | `owned_newsletter_hold`. | Imported contacts, mixed tester/newsletter consent, unsolicited sends, using newsletter before owned-audience gate. | `https://support.substack.com/hc/en-us/articles/4404340396564-How-do-I-report-a-content-violation` |

### Niche Press, Forums, And Database Adjacent Surfaces

| Surface | Use | Entry asset | Default route | Hard blockers | Source |
|---|---|---|---|---|---|
| Alpha Beta Gamer | Alpha/beta/demo submission when public/free access or key route exists. | Public free signup/download or review-access route, screenshots/video, short pitch, known issues. | `demo_press_submission_hold`. | No playable artifact, paid-only route without giveaway/key policy, no footage/images, expecting guaranteed coverage. | `https://www.alphabetagamer.com/contact-us/` |
| GamingOnLinux | Linux/SteamOS/Deck-adjacent press only after platform proof exists. | Linux/Proton/Steam Deck proof, official info, images/video, correction-ready contact. | `linux_press_hold`. | No Linux/SteamOS proof, AI-generated content hook, unsupported performance claim, generic press release. | `https://www.gamingonlinux.com/contact-us/`, `https://www.gamingonlinux.com/about-us/` |
| Product Hunt | Kill by default for the game. Consider only for a real dev tool, public utility, or playable product with PH fit. | Usable product, launch path, maker account custody, non-game audience fit. | `non_game_launch_kill_by_default`. | Game page as PH growth hack, vaporware, no useful product, upvote farming. | `https://help.producthunt.com/en/articles/9883485-product-hunt-featuring-guidelines` |
| NeoGAF / ResetEra | Monitor and answer only if an existing thread directly involves HECTON-8. Do not start self-promo threads. | Existing discussion, developer disclosure, factual answer, no CTA. | `high_risk_forum_monitor`. | New thread promoting HECTON, pasted PR, referral/store links, arguing with forum sentiment. | `https://www.neogaf.com/help/terms/`, `https://www.resetera.com/threads/general-guide-to-resetera.9777/` |
| Something Awful | Monitor only by default; paid ad route requires explicit human approval. | Existing relevant discussion or paid-ad quote; no stealth participation. | `forum_monitor_or_paid_ad_hold`. | Drive-by promo, stealth account, no paid-ad approval, no community fit. | `https://forums.somethingawful.com/forum_rules.php`, `https://www.somethingawful.com/forumrules/supportfaq.htm` |
| IGDB | Factual database maintenance after public source exists. | Official page, release/platform facts, screenshots/video rights, description source. | `database_listing`. | Promotional copy, unsourced facts, duplicate/incorrect title data. | `https://www.igdb.com/content-policy` |

### V3 Priority

1. `STEAM_NATIVE_PREP`: Curator Connect, Playtest, Demo, and Next Fest wait for exact Steam gates, build proof, support owner, and access/CTA permissions.
2. `SECONDARY_STOREFRONT_HOLD`: Epic and GOG wait for build/package, account custody, platform review/compliance, and support route.
3. `CREATOR_STREAM_HOLD`: Twitch/Kick/YouTube creator work waits for build/access, disclosure, known issues, and manual recipient fit.
4. `SOCIAL_MICRO_PITCH_HOLD`: X/Bluesky/Instagram/LinkedIn/Mastodon wait for account custody, real media, post gate, same-day rules, and non-spam cadence.
5. `TECHNICAL_PUBLISHING_HOLD`: Medium/Hashnode/DEV/Substack stay technical/owned-audience only; no SEO spam, no mixed consent.
6. `HIGH_RISK_FORUM_MONITOR`: NeoGAF/ResetEra/Something Awful are not launch channels. Answer existing discussion only, or keep monitor-only.

## 2026-05-26 PR Surface Expansion V4

Evidence boundary: additional Steam event/traffic, media-showcase, indie-showcase, and press-tip surfaces checked on 2026-05-26. This is scouting only. It does not authorize Steam event registration, broadcasts, widgets, visibility rounds, showcase nomination, press pitch, publication, public CTA, or spend.

### Steam Event, Traffic, And Measurement Surfaces

| Surface | Use | Entry asset | Default route | Hard blockers | Source |
|---|---|---|---|---|---|
| Steam Themed Sale Events | Future event eligibility scout for survival/crafting/horror/ocean-adjacent fests after Steam page tags and demo/page proof exist. | Steam page tags, store description truth, capsule/trailer, demo or upcoming section fit, event invite/registration page. | `steam_event_registration_hold`. | No Steam page, tag mismatch, event theme mismatch, no invite/registration proof, discount/event copy treated as guaranteed featuring. | `https://partner.steamgames.com/doc/marketing/upcoming_events/themed_sales?language=english`, `https://partner.steamgames.com/doc/marketing/upcoming_events?language=english` |
| Steam Broadcast / Store Livestream | Store-page or event livestream after demo/build and OBS/broadcast permissions exist. | Broadcast permission, demo/build, stream plan, spoiler policy, moderator, support owner, rebroadcast/live labeling. | `steam_broadcast_hold`. | No Steam page, no broadcast permission, stream is fake-live without labeling, no moderation, no demo/build stability. | `https://partner.steamgames.com/doc/store/broadcast?l=english`, `https://partner.steamgames.com/doc/store/broadcast/setting_up?l=german&language=english` |
| Steam UTM / Store Widget | Measurement and owned-site/embed route after public Steam page exists. | Steam app/page, UTM plan, widget location, source naming, CTA gate, privacy/consent note where needed. | `steam_measurement_hold`. | No public Steam page, UTM without source naming, widget placed before CTA gate, reporting visits as demand proof. | `https://partner.steamgames.com/doc/marketing/utm_analytics?language=english`, `https://partner.steamgames.com/doc/marketing/widget` |
| Steam Visibility / Wishlists | Internal measurement context only. Use to plan timing; do not sell "wishlist count" as proof. | Steam page, release/update plan, wishlist reporting, launch/update visibility source. | `steam_measurement_hold`. | Fake wishlist chase, visibility round assumed before eligibility, public claim based on raw wishlists, launch timing decided without build readiness. | `https://partner.steamgames.com/doc/marketing/visibility?language=english`, `https://partner.steamgames.com/doc/marketing/wishlist?l=english&title=partner.steamgames.com` |

### Media Showcase And Indie Showcase Surfaces

| Surface | Use | Entry asset | Default route | Hard blockers | Source |
|---|---|---|---|---|---|
| Future Games Show | High-bar media showcase candidate after trailer/demo/presskit proof and nomination route exist. | Polished trailer/gameplay, demo or release window, presskit, nomination/contact route, embargo owner. | `showcase_media_nomination_hold`. | No trailer, no demo/current footage, no news beat, no nomination route, expecting placement from cold email. | `https://www.futuregamesshow.com/`, `https://www.futuregamesshow.com/about`, `https://www.gamesradar.com/future-games-show/` |
| PC Gaming Show / PC Gamer events | PC-focused showcase/press route only after strong PC proof and event route exists. | PC trailer/gameplay, build/platform truth, presskit, PC-specific hook, editorial contact route. | `showcase_media_nomination_hold`. | No PC-specific hook, no trailer/demo, no presskit, trying to buy editorial through generic contact. | `https://www.pcgamingshow.com/`, `https://www.pcgamer.com/about-pc-gamer/` |
| The MIX | Indie showcase/event candidate after demo/trailer/current gameplay and fee/terms are checked. | Trailer, demo/build, screenshots, Steam/page truth, event/fee/submission state. | `indie_showcase_submission_hold`. | No build/video, fee not approved, no event window, weak asset, no human owner for follow-up. | `https://mediaindieexchange.com/showcases/`, `https://archive.mediaindieexchange.com/faq/` |
| Day of the Devs | Curated indie showcase candidate if HECTON has a distinct playable/gameplay proof package. | 2-3 minute intro video, playable demo/full ideal state, uniqueness answer, presskit. | `indie_showcase_submission_hold`. | No playable/demo, no clear "new and exciting" proof, no intro video, asset reads derivative. | `https://www.dayofthedevs.org/submit`, `https://www.dayofthedevs.org/` |
| Six One Indie | Indie showcase monitor/submit route after current official window and gameplay package exist. | Trailer/gameplay, Steam/page truth, submission window, demo if required. | `indie_showcase_submission_hold`. | Submissions closed, no current gameplay, no Steam/demo support, no post-show plan. | `https://www.sixoneindie.com/showcase`, `https://www.sixoneindie.com/` |
| INDIE Live Expo | Global multilingual indie showcase route after trailer/game info and official entry window. | Game info, trailer/gameplay, language-ready facts, Steam/page links if allowed, submission window. | `indie_showcase_submission_hold`. | Entry closed, no trailer/current gameplay, regional language facts missing, no owner for JP/EN follow-up. | `https://indie.live-expo.games/en/entry/`, `https://indie.live-expo.games/entry/` |
| Wholesome Direct / Wholesome Games | Kill by default for HECTON unless a specific hopeful/constructive angle genuinely fits without diluting pressure-noir identity. | Only a real build angle that is explicitly hopeful/constructive without lying about tone. | `tone_mismatch_kill_by_default`. | HECTON pitch becomes cozy, hopeful, cute, or warmth-first; pressure/noir identity is softened to fit the showcase. | `https://wholesomegames.com/` |
| Develop:Brighton Indie Showcase | UK industry showcase candidate after gameplay/build and innovation/originality proof exist. | Build/gameplay, video, originality proof, deadline/eligibility source, submission owner. | `indie_showcase_submission_hold`. | Deadline missed, no playable/video proof, no originality answer, no travel/ops owner. | `https://www.developconference.com/whats-on/indie-showcase-competition` |

### Press-Tip And Editorial Surfaces

| Surface | Use | Entry asset | Default route | Hard blockers | Source |
|---|---|---|---|---|---|
| PC Gamer editorial | PC press tip after newsworthy public beat, not general awareness. | Presskit, trailer/demo, PC-specific hook, embargo if needed, official inbox. | `mainstream_press_tip_hold`. | No news beat, no presskit, no playable/footage proof, unsupported performance claim, mass mail. | `https://www.pcgamer.com/about-pc-gamer/` |
| GamesRadar+ / Future editorial | General game press route or Future-showcase adjacent only after a strong trailer/news beat. | Presskit, trailer, exact contact/nomination route, embargo owner. | `mainstream_press_tip_hold`. | Generic "please cover us", no trailer, no public page, fake exclusivity, no source for contact route. | `https://www.gamesradar.com/about-gamesradar/`, `https://www.futuregamesshow.com/` |
| PCGamesN | PC audience press route if hook is PC-specific, technical, survival, or systems-driven. | Presskit, PC proof, trailer/demo, clear news angle. | `niche_pc_press_tip_hold`. | No PC-specific reason, no asset, no presskit, paid-ad route confused with editorial. | `https://www.pcgamesn.com/about-us` |
| GameSpot | Broad games press monitor/tip route after public trailer/demo and news beat exist. | Presskit, trailer/demo, public page, factual pitch, embargo owner if needed. | `mainstream_press_tip_hold`. | No news beat, no public page, no footage, key request without access policy. | `https://www.gamespot.com/about/` |
| Pocket Gamer | Mostly mobile/tablet route; hold unless a real mobile/handheld story exists. | Mobile/handheld proof, store page, trailer, contact route. | `platform_mismatch_hold`. | No mobile/handheld plan, generic PC pitch, no platform proof. | `https://www.pocketgamer.com/pages/contact-us/` |

### V4 Priority

1. `STEAM_EVENT_PREP`: Steam themed events, broadcast, UTM/widget, visibility, and wishlists stay measurement/event-prep only until Steam page/demo/build gates exist.
2. `SHOWCASE_PACKAGE_PREP`: Future Games Show, PC Gaming Show, The MIX, Day of the Devs, Six One Indie, INDIE Live Expo, and Develop:Brighton need a trailer/demo/current gameplay package and deadline/source check.
3. `TONE_MISMATCH_KILL`: Wholesome routes are kill-by-default unless HECTON can fit without weakening pressure/noir identity.
4. `PRESS_TIP_HOLD`: PC Gamer, GamesRadar/Future, PCGamesN, GameSpot, and Pocket Gamer need a real news beat, presskit, footage/demo, and official inbox custody.

## 2026-05-26 PR Surface Expansion V5

Evidence boundary: additional regional, physical, hybrid, B2B, and award surfaces checked on 2026-05-26. This is scouting only. It does not authorize application, travel, booth reservation, payment, business meetings, award entry, public claim, or public CTA.

### Regional And Physical Showcase Surfaces

| Surface | Use | Entry asset | Default route | Hard blockers | Source |
|---|---|---|---|---|---|
| PAX Rising Showcase | Curated PAX floor visibility after playable booth demo and ops owner exist. | Playable demo, trailer, presskit, booth staffing plan, hardware/shipping plan, PAX application window. | `physical_showcase_hold`. | No playable booth demo, no travel/ops budget, no hardware owner, no 3-4 month application-window check, no post-show lead plan. | `https://west.paxsite.com/en-us/features/pax-rising-showcase.html` |
| PAX Aus Indie Showcase | Kill unless team eligibility matches Australia/New Zealand indie requirements; otherwise monitor only. | Eligibility proof, beta-state build, entry fee approval, booth/demo plan. | `regional_eligibility_kill_by_default`. | Team not Australia/New Zealand based, no beta build, no fee approval, no booth ops owner. | `https://aus.paxsite.com/en-us/get-involved/ais.html`, `https://aus.paxsite.com/en-us/get-involved/ais/terms-conditions.html` |
| BitSummit | Japan indie festival route after demo/trailer and JP/EN event follow-up owner exist. | Current gameplay/trailer, playable demo if required, Steam/page truth, JP/EN facts, travel/remote showcase plan. | `regional_indie_showcase_hold`. | Submission window closed, no JP/EN owner, no playable/current media, no travel/remote coverage owner. | `https://bitsummit.org/en/submissions-now-open-bitsummit-punch-may-22-24-2026-at-miyako-messe-kyoto/` |
| Tokyo Game Show Selected Indie / Indie Game Area | Asia/Japan industry/consumer route after TGS eligibility, video, language, booth, and ops proof. | Application guide, video, game build or demo state, JP/EN assets, travel/booth owner. | `regional_indie_showcase_hold`. | No JP/EN materials, no video, no booth/travel plan, selected-indie eligibility not proven. | `https://4c281b16296b2ab02a4e0b2e3f75446d.cdnext.stream.ne.jp/tgs/2026/exhibition/common/en/b21_exhibitor_guide_en.pdf`, `https://4c281b16296b2ab02a4e0b2e3f75446d.cdnext.stream.ne.jp/tgs/2026/exhibition/common/en/d24_sown_en.pdf` |
| Taipei Game Show Indie Game Award / Indie House | Asia award/showcase route after build/video and regional follow-up owner exist. | Award/submission rules, video/build, English/Chinese facts if needed, booth/business plan. | `regional_award_showcase_hold`. | No regional owner, no build/video, no submission source, no booth/support plan. | `https://tgs.tca.org.tw/files/Regulations%20and%20FAQ.pdf`, `https://gamedaily.com/news/taipei-game-show-launches-indie-global-tour-opens-entries-for-indie-game-awards-2026` |
| gamescom Indie Arena Booth / Home of Indies | Major EU physical showcase route after demo, booth budget, travel, and ops proof exist. | Current demo, trailer, Steam/page truth, submission source, booth package, travel/ops owner. | `physical_showcase_hold`. | No demo, no booth/travel budget, no hardware staffing plan, no deadline/source proof, relying on gamescom presence as PR by itself. | `https://letscodegames.com/en/submission/submission`, `https://www.gamescom.global/en/program/gamescom-award`, `https://home-of-indies.com/` |
| Calgary Indie Game Bash | Local/niche public showcase route; monitor unless regional travel/community owner and playable station proof exist. | Current playable build, local event source, booth/demo plan, travel owner, follow-up owner. | `regional_showcase_monitor`. | No local owner, no playable station, no travel/budget, no post-event lead plan. | `https://www.calgaryundergroundfilm.org/2026/indie-game-bash/` |

### B2B, Industry, And Award Surfaces

| Surface | Use | Entry asset | Default route | Hard blockers | Source |
|---|---|---|---|---|---|
| ChinaJoy x Game Connection | B2B/Asia publisher-investor route after business deck, build, and regional/legal owner exist. | Pitch deck, build/demo, business terms, China/Asia market owner, meeting plan. | `b2b_event_hold`. | No build, no deck, no legal/regional owner, booth/payment without budget approval, no follow-up CRM. | `https://www.game-connection.com/about-game-connection-x-chinajoy/`, `https://www.game-connection.com/zh/submit-your-projects-gcxcj2026-zh/` |
| Nordic Game | Northern Europe industry/B2B/expo route after meeting goals and business package exist. | Business deck, demo, meeting target list, travel/ops budget, follow-up owner. | `b2b_event_hold`. | No business target, no travel/budget, no demo, no follow-up owner. | `https://nordicgame.com/wp-content/uploads/2025/10/Nordic_Game_Introduction.pdf` |
| Reboot Develop Blue Indie Expo | Monitor/hold until current official submission path and cost are confirmed. | Current rules, cost, demo, travel/booth owner. | `physical_showcase_monitor`. | Only stale rule source exists, no current application source, no travel/ops owner. | `https://rebootdevelopblue.com/wp-content/uploads/2022/10/indie_expo_area_rules_and_regulations_2023.pdf` |
| Debug Indie Game Awards | Award route after public/releasable quality, category fit, and submission window. | Category fit, build/media, fee/deadline, public facts, award owner. | `award_submission_hold`. | Submission closed, no category fit, no media/build proof, award claim used before nomination. | `https://www.teamdebug.com/awards` |
| BostonFIG Figgie Awards | Festival/award route after playable proof and AI/tool disclosure readiness. | Submission guidelines, playable build, fee approval, AI/tool disclosure, festival owner. | `award_submission_hold`. | No playable, no disclosure answer, fee not approved, not accepted yet but public copy implies selection. | `https://www.bostonfig.com/festival-2026/submission-guidelines/` |
| IndieGameBusiness Pitch Your Game LIVE | Business pitch/feedback route only if pitch deck and ask are explicit. | Pitch deck, build/demo, one concrete business ask, submission deadline/source. | `b2b_pitch_event_hold`. | No pitch ask, no demo, generic PR pitch, deadline missed. | `https://indiegamebusiness.com/resources/pitch-your-game-live/` |
| GDC Pitch | Publisher/investor pitch route only after booth/presence eligibility, business deck, and legal owner exist. | Pitch deck, current demo, explicit ask, GDC presence/eligibility source, legal/business owner. | `b2b_pitch_event_hold`. | No eligible presence, no deck, no demo, no business terms, no follow-up CRM. | `https://gdconf.com/gdc-pitch/` |
| Game Gauntlet | Accelerator/festival route only if current build and accelerator fit exist. | Build, application source, accelerator fit, follow-up owner. | `accelerator_festival_hold`. | No fit, no build, no accelerator follow-up owner, public claim before finalist/acceptance. | `https://www.gamegauntlet.gg/` |

### V5 Priority

1. `PHYSICAL_SHOWCASE_HOLD`: PAX, gamescom, BitSummit, TGS, Taipei, Calgary, and Reboot require playable booth/demo proof, travel/booth/hardware/staffing owner, and deadline/source proof.
2. `REGIONAL_ELIGIBILITY_KILL`: Regional eligibility rules can kill a route immediately; do not invent local presence.
3. `B2B_EVENT_HOLD`: ChinaJoy/Game Connection, Nordic Game, GDC Pitch, and pitch events require business deck, explicit ask, meeting target list, legal owner, and follow-up CRM.
4. `AWARD_SUBMISSION_HOLD`: Debug/BostonFIG/award routes need category fit, build/media proof, fee/deadline source, and no public selection language before acceptance.
5. `REMOTE_OR_MONITOR_ONLY`: Any physical event without budget, travel, booth, hardware, support, and post-show owner stays monitor-only.

## 2026-05-26 PR Surface Expansion V6

Evidence boundary: additional genre, direct, art-game, narrative, horror, Steam-festival, and local-indie showcase surfaces checked on 2026-05-26. This is scouting only. It does not authorize festival application, Steam event participation, publisher pitch, award entry, booth attendance, public selection claim, or public CTA.

### Genre, Direct, And Steam-Festival Surfaces

| Surface | Use | Entry asset | Default route | Hard blockers | Source |
|---|---|---|---|---|---|
| A MAZE. Berlin | Art-game/playful-media award and exhibition route only if HECTON has a genuinely experimental, interactive, or installation-readable asset. | Build/prototype, trailer/video, art/interaction statement, fee approval, optional onsite plan. | `art_experimental_award_hold`. | Reads as normal commercial survival, no experimental proof, no fee/deadline source, public award/nominee claim before acceptance. | `https://2026.award.amaze-berlin.de/`, `https://2026.award.amaze-berlin.de/sites/default/files/2025-12/Conditions%20of%20Participation%202026.pdf` |
| gamescom latam BIG Festival | Latin America-facing global indie competition/showcase route after playable build and regional follow-up owner exist. | Playable build, trailer, Steam/page truth, English/Portuguese/Spanish facts if needed, regional follow-up owner. | `regional_indie_showcase_hold`. | No playable build, no regional language/support owner, no current submission source, finalist benefits treated as guaranteed. | `https://latam.gamescom.global/en/big-festival-en/` |
| AdventureX | UK narrative-game convention route only if the asset proves narrative-driven play, not just lore. | Narrative gameplay clip, playable build, London exhibit/speaker source, travel/ops owner. | `digital_narrative_festival_hold`. | No narrative mechanics, no UK event owner, no exhibit/speaker route source, survival systems drown the narrative angle; do not use `adventure-x.org` hackathon FAQ as UK Adventure X proof. | `https://www.adventurexpo.org/` |
| LudoNarraCon | Steam digital narrative festival route only if HECTON can prove story-rich gameplay and Steam/demo readiness. | Steam page/app, demo/build state, narrative hook, trailer, application timing, Fellow Traveller/LNC source. | `digital_narrative_festival_hold`. | Steam page/demo not ready, narrative angle is lore-only, no application source, event hashtag treated as permission. | `https://www.ludonarracon.com/` |
| Cerebral Puzzle Showcase | Steam puzzle/thinking-game event; kill unless a real frequency/signal/puzzle mechanic is central and playable. | Steam page/app, demo, puzzle mechanic proof, event source. | `steam_genre_festival_kill_by_default`. | Core game is not puzzle/thinking-first, puzzle is side activity, no Steam/demo proof. | `https://www.cerebralpuzzleshowcase.com/`, `https://store.steampowered.com/sale/CerebralPuzzleShowcase2025` |
| Women-Led Games / women-led showcases | Identity-eligibility route; kill unless team leadership eligibility is source-proven and owner-approved. | Eligibility proof, current media, application/source, public statement owner. | `identity_eligibility_kill_by_default`. | Team eligibility not proven, identity used as opportunistic PR, no owner approval. | `https://itch.io/jam/wgf2026`, `https://www.maxi-geek.com/event-details/women-led-gaming-showcase-2026` |

### Horror, Publisher, And Local Indie Surfaces

| Surface | Use | Entry asset | Default route | Hard blockers | Source |
|---|---|---|---|---|---|
| DreadXP Pitch | Horror publisher pitch route only if HECTON is legitimately horror-themed and has a build/deck/budget/timeline. | Playable build, pitch deck, development timeline, budget, trailer/screens/gifs, team profile. | `publisher_pitch_hold`. | No playable build, no deck, no budget/timeline, pitch reframes HECTON as pure horror instead of pressure/noir survival. | `https://dreadxp.com/how-to-pitch-dreadxp/` |
| DreadXP / The MIX Indie Horror Showcase | Horror/horror-adjacent trailer route after current trailer and AI/tool disclosure pass. | Trailer, gameplay media, horror-adjacent fit statement, AI/tool disclosure, submission source. | `genre_showcase_hold`. | No current trailer, no horror fit, any generative-AI eligibility issue, public selection claim before acceptance. | `https://www.dreadcentral.com/news/540789/dreadxp-is-bringing-the-screams-this-october-with-the-3rd-annual-indie-horror-showcase/`, `https://www.indiehorrorshowcase.com/` |
| MAGFest MIVS | Physical indie showcase route after playable copy/video and booth stamina proof exist. | Copy of game, 30s uninterrupted gameplay video under 2 min, booth staffing, PG-15/original-assets policy check. | `physical_indie_showcase_hold`. | No playable copy/video, no booth staffing, AI/original-asset policy issue, content rating mismatch, public announcement before acceptance. | `https://super.magfest.org/mivs` |
| DreamHack Indie Playground | Local/physical indie playground and Steam event route; monitor unless local booth station and Steam event proof exist. | Playable station, Steam page/event state, local event source, travel/staffing owner. | `physical_indie_showcase_monitor`. | No application/source path, no local owner, no station plan, treating community vote/awards as guaranteed. | `https://dreamhack.com/atlanta/indie-playground/` |

### V6 Priority

1. `GENRE_FIT_FIRST`: Narrative, art, horror, puzzle, and identity showcases are not neutral exposure. If HECTON does not honestly fit the theme, route is `KILL`.
2. `STEAM_FESTIVAL_HOLD`: Steam-hosted narrative/puzzle/direct events require Steam page/app state, demo/build proof, event source, and CTA gate separation.
3. `PUBLISHER_PITCH_HOLD`: DreadXP/publisher-style routes need build, deck, budget, timeline, team profile, and deal boundaries.
4. `LOCAL_PHYSICAL_HOLD`: MAGFest/DreamHack-style routes require station stability, content policy, staffing, travel, hardware, and acceptance proof.
5. `NO_LAUREL_BEFORE_ACCEPTANCE`: Selection, nomination, finalist, award, Steam event participation, and showcase attendance language is forbidden before acceptance proof.

## 2026-05-26 PR Surface Expansion V7

Evidence boundary: additional key-distribution, PR-tooling, presskit, newswire, industry-showcase, award, and market-data surfaces checked on 2026-05-26. This is scouting only. It does not authorize key upload, creator access, presskit publication, paid PR tooling, press release send, award submission, B2B meeting, public claim, account action, or public CTA.

### Key, Presskit, And Creator-Distribution Surfaces

| Surface | Use | Entry asset | Default route | Hard blockers | Source |
|---|---|---|---|---|---|
| Keymailer | Creator discovery and code-request tooling after demo/build access and manual approval policy exist. | Build/demo, key batch, presskit, creator criteria, disclosure copy, revocation/escalation plan. | `creator_key_distribution_hold`. | No private-access gate, no key batch owner, automatic approval, no official inbox custody, no manual recipient fit, no grey-market monitoring. | `https://keymailer.co/` |
| Lurkit | Creator campaigns, missions, or paid creator ops after build, campaign owner, and disclosure policy exist. | Build/demo, campaign brief, budget if paid, creator filters, known issues, disclosure, support owner. | `creator_key_distribution_hold`. | No campaign owner, no build, no budget boundary, paid/organic mixed, no support route, no access/provenance fields. | `https://campaigns.lurkit.com/companies-quests`, `https://support.lurkit.com/migration/creator-program-setup` |
| Terminals.io | Presskit, code distribution, coverage tracking, and media/creator request route after presskit and access policy exist. | Presskit, game page facts, code policy, official inbox, known issues, access log. | `presskit_distribution_hold`. | No presskit, no official contact, no code policy, no access-log route, no publisher/developer owner. | `https://www.terminals.io/support/faq`, `https://blog.terminals.io/posts/requesting-keys` |
| PressEngine | PR platform for press release, code distribution, event management, and media verification after a real news beat exists. | Presskit URL, release/news beat, code policy, media list rules, official inbox, budget decision. | `pr_tooling_hold`. | No news beat, no presskit, no contact email, paid spend unapproved, treating platform verification as send permission. | `https://pressengine.net/articles/freeaccount`, `https://pressengine.net/` |
| Game.Press | Press/creator key request and press access route; hold until public facts and access owner exist. | Presskit, game page facts, code policy, media/creator criteria, official inbox. | `presskit_distribution_hold`. | No presskit, no build/access state, no owner for requests, no disclosure/access log. | `https://game.press/`, `https://www.game.press/help/` |
| Key Lynx | Low-cost indie key distribution and creator discovery monitor route; use only after manual approval policy exists. | Build/demo, key batch, creator filters, support owner, tracking sheet. | `creator_key_distribution_hold`. | Free/cheap tool treated as readiness, no key cap, no creator verification review, no grey-market watch. | `https://keylynx.gg/` |
| Games Press | Press-release/newswire upload route after real news beat, presskit, and claim lint exist. | Plain-text release, images/links, embargo if any, presskit, official inbox, news beat. | `press_distribution_hold`. | PDF-only release, no news beat, no presskit, embargo unclear, print-quality asset policy ignored. | `https://www.gamespress.com/tr/How-to-Submit-News` |
| IndieGames.Press | Small press/creator submission route; hold until press release/media and no-guarantee expectation are clear. | Press release or link, cover image, platform/genre facts, promotion type, contact owner. | `demo_press_submission_hold`. | No press release, no real media, paid promotion blurred with editorial, expecting guaranteed redistribution. | `https://www.indiegames.press/` |

### Industry, Award, And Discovery Surfaces

| Surface | Use | Entry asset | Default route | Hard blockers | Source |
|---|---|---|---|---|---|
| IGF | Major award route for independent, innovative work; next-cycle monitor until current submission window/source exists. | Playable build or access code, video link, category/eligibility, fee/waiver decision, deadline owner. | `major_award_submission_hold`. | Window closed/stale, no playable, no fee approval, public finalist/award claim before acceptance. | `https://igf.com/submit-your-game/`, `https://gdconf.com/igf/` |
| IndieCade | Festival/award route for independent and experimental games after documentation and playable proof exist. | Playable build, documentation, video/media, fee/deadline, category fit, disclosure if needed. | `award_submission_hold`. | No documentation, no playable/media, submission year unclear, selection language before proof. | `https://www.indiecade.com/submissions/` |
| Digital Dragons Indie Zone / Indie Dragons Awards | CEE industry/showcase/award route after playable build, business follow-up, and rules proof exist. | Playable version, trailer/media, award/showcase rules, meeting target, travel/remote owner. | `industry_showcase_hold`. | Only old rules used, no playable, no CEE/business follow-up owner, no current competition rule proof. | `https://conference.digitaldragons.pl/indie-zone-2026/`, `https://konferencja.digitaldragons.pl/wp-content/uploads/sites/2/2025/11/DD-Conference-2026-competition-rules.pdf` |
| INDIGO | Benelux business/showcase route after hands-on demo, publisher/investor goal, and follow-up owner exist. | Playable demo, pitch one-pager, target list, travel/remote owner, follow-up CRM owner. | `industry_showcase_hold`. | No business goal, no playable, no follow-up owner, regional travel assumed. | `https://indigoshowcase.nl/` |
| MDEV Showcase | Midwest public/industry local showcase route after station demo and travel/ops owner exist. | Playable station, crash/restart plan, hardware owner, travel/staffing owner, contact plan. | `physical_showcase_hold`. | No station build, no local travel/ops owner, no support plan, submission treated as guaranteed feature. | `https://www.mdevconf.com/` |
| XP Game Summit / Indie Spotlight | Canada B2B ecosystem route after business target and showcase path are source-checked. | Pitch deck, demo, meeting target, Indie Pod/showcase source, CRM follow-up. | `b2b_event_hold`. | No business ask, no demo, no current showcase route, no follow-up owner. | `https://xpgamesummit.com/`, `https://xpgamesummit.com/who-attends/`, `https://xpgaming.biz/introducing-xp-indie-spotlight-xp-event-updates/` |
| Indie Cup | Online indie festival route after gameplay footage and Round II playable-build readiness exist. | 5 min current gameplay footage, playable build plan, category fit, official window source. | `indie_showcase_submission_hold`. | No current gameplay footage, no playable for later round, public selection claim before acceptance. | `https://indiecup.net/`, `https://indiecup.net/rules/` |
| GameDiscoverCo | Market-data/newsletter monitor only; not an outreach or press route until a real submit/contact reason exists. | Public Steam/page facts, market question, monitor note, no CTA. | `market_data_monitor`. | Treating data/newsletter mention as demand proof, pitching with no news beat, paid data used as public metric. | `https://gamediscover.co/` |

### V7 Priority

1. `KEYS_ARE_LIABILITY`: Key and creator hubs stay `HOLD` until private-access gate, key batch cap, manual approval, disclosure, access log, support owner, and grey-market monitoring exist.
2. `PRESSKIT_BEFORE_PRESSWIRE`: Games Press, Game.Press, Terminals, PressEngine, and IndieGames.Press require presskit, real news beat or demo access truth, official inbox custody, and claim lint.
3. `AWARD_IS_NOT_MARKETING_PROOF`: IGF, IndieCade, Indie Cup, and Digital Dragons require playable/media/category/fee/deadline proof and no selection language before acceptance.
4. `B2B_NEEDS_AN_ASK`: INDIGO, XP, and Digital Dragons business routes need a deck, explicit business ask, target list, legal/business owner, and CRM follow-up.
5. `DATA_IS_NOT_DEMAND`: GameDiscoverCo and similar data/newsletter surfaces can shape market questions, not public demand claims.

## 2026-05-26 PR Surface Expansion V8

Evidence boundary: additional platform-holder, handheld/cloud, publisher-pitch, and funding surfaces checked on 2026-05-26. This is scouting only. It does not authorize console claims, platform applications, devkit requests, cloud opt-in, storefront publication, publisher pitch, funding application, public platform logo use, account action, or public CTA.

### Platform Holder, Storefront, Handheld, And Cloud Surfaces

| Surface | Use | Entry asset | Default route | Hard blockers | Source |
|---|---|---|---|---|---|
| ID@Xbox / Microsoft Game Dev | Xbox/Windows publishing program route after PC build truth, controller UX, business entity/account, and platform scope owner exist. | Build/demo, concept/application data, platform target, controller UX proof, compliance owner, business/legal owner. | `platform_holder_program_hold`. | Treating ID@Xbox as PR, no platform owner, no controller/disconnect/accessibility proof, no business account/legal owner, no Xbox build plan. | `https://developer.microsoft.com/en-us/games/articles/2023/11/publishing-pathways-id-at-xbox/`, `https://learn.microsoft.com/es-es/gaming/game-publishing/publishing-processes/managed-creators/publishing-processes-onboarding-new-creator` |
| PlayStation Partners | PlayStation platform route only after console scope, legal/company custody, port budget, and certification owner exist. | Game build/prototype, company account, pitch/platform plan, controller/performance proof, compliance owner. | `platform_holder_program_hold`. | No company/legal owner, no port budget, no certification plan, using "PlayStation" as public credibility before approval. | `https://partners.playstation.net/` |
| Nintendo Developer Portal | Nintendo Switch/eShop self-publishing route only after company account, platform compliance, port budget, and content/age-rating owner exist. | Company account, build/prototype, eShop plan, controller/handheld UX, rating/compliance owner. | `platform_holder_program_hold`. | No company account, no handheld/perf proof, no rating/compliance owner, public Nintendo/eShop claim before approval. | `https://developer.nintendo.com/`, `https://publisher.nintendo.com/` |
| Epic Games Store publishing | Secondary PC storefront route after Steam-truth parity, build/package, achievements parity, support, and account custody exist. | Build package, store assets, achievements/compliance plan, support owner, account custody, Steam parity check. | `secondary_storefront_hold`. | No build/package, no achievement parity answer, no support owner, exclusivity/first-run decision unapproved, page implies release truth. | `https://store.epicgames.com/en-US/publish` |
| Steam Deck compatibility review | Handheld compatibility proof route, not a marketing claim, after Steam page/app and real Deck/Proton testing exist. | Steam app, build, controller-only UX, text-input proof, readable UI, performance capture, known issues. | `handheld_compatibility_hold`. | No Steam app, no Deck/Proton test, unreadable UI/text, performance unsupported, claiming Verified before Valve result. | `https://partner.steamgames.com/doc/steamdeck/compat?l=english`, `https://www.steamdeck.com/en/verified` |
| GeForce NOW Developer Platform | Cloud streaming opt-in/integration route after store ownership, support, save/login, and streaming UX proof exist. | Store app, build, account/linking answer, cloud-save state, support owner, opt-in/source proof. | `cloud_distribution_hold`. | No store ownership path, login/save unsupported, no GFN owner, cloud availability claimed before opt-in/approval. | `https://developer.nvidia.com/join-geforce-now-dev-program`, `https://nvidia.custhelp.com/app/answers/detail/a_id/5360/~/how-to-request-a-new-game-be-added-to-geforce-now` |
| Amazon Luna | Monitor-only cloud route after 2026 third-party purchase/support changes; do not plan as standard PC distribution. | Current official developer route, platform strategy, catalog/partnership proof. | `cloud_platform_monitor`. | Third-party access model unclear, no official onboarding path, claiming Luna availability, treating cloud as low-spec proof. | `https://developer.amazon.com/it/luna`, `https://www.aboutamazon.com/news/entertainment/amazon-luna-redesign-gamenight-prime` |

### Publisher, Funding, And Label Surfaces

| Surface | Use | Entry asset | Default route | Hard blockers | Source |
|---|---|---|---|---|---|
| Kowloon Nights | Premium PC/console funding route after playable proof, deck, budget, team plan, and release-management answer exist. | Playable build/proof, pitch deck, budget/runway, production plan, team roles, release/platform plan. | `funding_pitch_hold`. | No playable, no budget, no team/release plan, mobile/advergame/blockchain mismatch, expecting hands-on publishing. | `https://www.kowloonnights.com/contact-us`, `https://www.kowloonnights.com/` |
| Epic MegaGrants | Grant route only if HECTON has Unreal/Epic ecosystem relevance or a specific eligible technology/artifact. | Grant artifact, video, project summary, Epic relevance, budget use, legal owner. | `grant_application_hold`. | Unity-only game pitched as generic grant, no Epic relevance, no video/proof, treating grant as production certainty. | `https://www.fortnite.com/news/apply-to-get-an-epic-megagrant-for-your-uefn-project` |
| Outersloth | Indie fund monitor route; pitch only if official process, terms, and game-fit proof are current. | Official process, playable proof, budget, terms review, legal owner, no-AI/source-rights answer. | `funding_pitch_monitor`. | No current official route, no terms review, no budget, AI/source-rights ambiguity, treating fund as PR. | `https://innersloth.zendesk.com/hc/en-us/articles/27055721954196-I-have-questions-about-Outersloth` |
| Devolver Digital pitch | Publisher pitch route only after a sharp playable hook and public-ready weird/strong identity exist. | Playable build, pitch deck, trailer/gif, budget/timeline, deal boundaries, standout proof. | `publisher_fit_pitch_hold`. | No playable hook, no budget/timeline, generic survival pitch, trying to imitate Devolver tone. | `https://pitch.devolverdigital.com/`, `https://www.devolverdigital.com/` |
| Fellow Traveller | Narrative-publisher route only if narrative mechanics are central and playable, not lore-only. | Narrative build/prototype, pitch deck, story-system proof, timeline, budget, team profile. | `publisher_fit_pitch_hold`. | Narrative is lore-only, survival systems dominate, no build/prototype, no pitch owner. | `https://www.fellowtraveller.games/developers`, `https://www.fellowtraveller.games/contact/` |
| No More Robots | Data-driven publisher route after pitch document, build, and commercial positioning proof exist. | Pitch document, playable build, target market, pricing/window, Steam/page data if any. | `publisher_fit_pitch_hold`. | No build, no pitch doc, no commercial data, pitch depends on vibes instead of hook/market. | `https://nomorerobots.io/` |
| Hooded Horse | Strategy/sim publisher route; kill unless HECTON's systems layer can honestly fit their catalogue direction. | Build/proof, systems pitch, market positioning, budget, team plan, no-AI/source-rights answer. | `publisher_fit_kill_by_default`. | Pressure/noir survival does not fit strategy/sim, no systems proof, AI/source-rights ambiguity, no build. | `https://hoodedhorse.com/submit-your-game/` |
| Team17 | Broad publisher/services route after playable build, website, company/legal owner, and platform/services ask exist. | Game website, playable build, pitch form, services ask, budget/timeline, company/legal owner. | `publisher_fit_pitch_hold`. | No game website/build, no explicit services ask, form used as generic awareness, no legal/business owner. | `https://www.team17.com/pitch-your-game`, `https://info.team17.com/submit-game/submit/` |

### V8 Priority

1. `PLATFORM_LOGOS_ARE_FORBIDDEN`: Xbox, PlayStation, Nintendo, Epic, Deck, GFN, and Luna names stay internal until approval/certification/source proof exists.
2. `CONSOLE_IS_A_PORT_SCOPE`: Platform-holder routes require port budget, compliance/cert owner, controller UX, performance proof, business/legal owner, and support plan.
3. `CLOUD_IS_NOT_LOW_SPEC_PROOF`: Cloud routes can expand access, but they do not prove MX350/Steam Deck/low-end performance.
4. `PUBLISHER_PITCH_NEEDS_REAL_ASK`: Publishers/funds require playable proof, deck, budget/timeline, deal boundaries, legal owner, and exact reason this partner fits.
5. `GRANTS_ARE_NOT_RUNWAY`: Grant/fund applications cannot be production plan, marketing beat, or public credibility before award/contract proof.

## 2026-05-26 PR Surface Expansion V9

Evidence boundary: additional regional storefront, RU media/community, reseller/publisher, and database/artwork catalog surfaces checked on 2026-05-26. This is scouting only. It does not authorize regional publication, store submission, SDK integration, paid media, editorial pitch, publisher pitch, database edit, artwork upload, account action, outreach, or public CTA.

### Regional Storefront And Platform Surfaces

| Surface | Use | Entry asset | Default route | Hard blockers | Source |
|---|---|---|---|---|---|
| VK Play developer dashboard | RU/CIS PC storefront route after build/package, account custody, legal/payment answer, community/support owner, and Steam-truth parity exist. | Build package, store assets, developer account owner, legal/payment owner, support/community owner, release-state parity. | `regional_storefront_hold`. | No build/package, no legal/payment route, no Russian support owner, page implies release/demo state before proof. | `https://documentation.vkplay.ru/hotbox/devdocs/pdfcopy/en/1098.pdf`, `https://documentation.vkplay.ru/hotbox/devdocs/pdfcopy/en/1102.pdf`, `https://documentation.vkplay.ru/hotbox/devdocs/pdfcopy/en/1103.pdf` |
| WeGame developer platform | China PC storefront route only with company/local-publisher/compliance owner and localization/support plan. | Company registration, local-publisher/GAPP path, build, SDK/compliance owner, localization/support owner, business decision. | `regional_storefront_hold`. | No company/legal route, no Chinese publisher/compliance path, no SDK owner, treating WeGame as simple upload or PR. | `https://developer.wegame.com/developer/static/faq_en.html`, `https://developer.wegame.com/gamereg/faqen.html` |
| TapTap Developer Center | Mobile/regional test/discovery route; kill by default for HECTON PC unless a real mobile/Android scope exists. | Approved developer account, mobile build/APK, region/status plan, support owner, platform scope approval. | `platform_mismatch_hold`. | PC-only game, no mobile build, no Android QA/support, APK trust risk, page used as broad hype. | `https://developer.taptap.io/docs/store/store-creategame/`, `https://developer.taptap.io/docs/store/store-devagreement/` |
| Green Man Gaming partner/publishing | Reseller/publishing/services route after Steam/store strategy, keys/revenue terms, support, and discount policy exist. | Steam app/build, commercial terms owner, key/discount policy, support owner, partner source, CRM route. | `reseller_distribution_hold`. | No Steam app/build, no pricing/key policy, no support route, reseller used as awareness without store proof. | `https://greenmangaming.zendesk.com/hc/en-us/articles/215465328-Want-to-sell-or-publish-your-game-with-Green-Man-Gaming`, `https://www.greenmangaming.com/company/publishing/` |

### RU Media, Community, And Business Surfaces

| Surface | Use | Entry asset | Default route | Hard blockers | Source |
|---|---|---|---|---|---|
| Pikabu | RU broad community/paid route only for no-link critique or paid ad review after Russian owner, disclosure, and rules check. | RU post draft, one real asset, same-day rules/ad rules, disclosure, reply/moderation owner. | `ru_broad_community_hold`. | Link-drop, disguised ad, no RU reply owner, no artifact, no same-day rules. | `https://pikabu.ru/information/rules`, `https://pikabu.ru/information/adrules` |
| vc.ru | RU business/dev article route after technical/business substance exists; paid/promoted route requires separate ad rules. | Technical/business article, source proof, no-link value, author/account owner, disclosure/ad route check. | `ru_business_article_hold`. | Pure game ad, no business/technical angle, booster/ad rules ignored, no RU reply owner. | `https://vc.ru/rules`, `https://vc.ru/booster-rules` |
| PlayGround.ru | RU game media/community/paid PR route after news beat, presskit, RU assets, disclosure, and paid/editorial distinction exist. | News beat, RU presskit/assets, contact/paid route source, claim lint, official inbox owner. | `ru_paid_media_hold`. | Paid PR disguised as editorial, no news beat, no RU assets, no official inbox, fake database/community profile. | `https://www.playground.ru/about/`, `https://www.playground.ru/reklama/` |
| Kanobu | RU editorial/commercial route after real news beat, RU presskit, and contact route separation exist. | RU presskit, trailer/screens, news beat, contact route, commercial/editorial distinction, claim lint. | `ru_editorial_tip_hold`. | No news beat, generic pitch, commercial route confused with editorial, unsupported platform/performance claims. | `https://kanobu.ru/games/contact/articles/` |
| StopGame | RU editorial/contact monitor route; pitch only with news beat and current contact/source proof. | News beat, RU presskit, contact form/source, official inbox, asset proof. | `ru_editorial_tip_hold`. | No route beyond generic contact, no news value, unsupported claims, repeated submissions. | `https://stopgame.ru/contact` |

### Publisher Resource, Database, And Artwork Catalog Surfaces

| Surface | Use | Entry asset | Default route | Hard blockers | Source |
|---|---|---|---|---|---|
| Secret Mode publishing | Publisher route after playable build/deck and fit proof; relevant only if HECTON fit is not rewritten toward cozy/wholesome. | Playable build, pitch deck, trailer/gif, fit statement, budget/timeline, deal boundaries. | `publisher_fit_pitch_hold`. | No build/deck, tone-fit mismatch, copying publisher tone, no explicit ask. | `https://wearesecretmode.com/publishing`, `https://wearesecretmode.com/` |
| tinyBuild pitch resource | Pitch-learning and possible publisher route after deck/build exist; resource is not a send gate by itself. | Pitch deck, easy-to-play build, trailer/gif, business ask, target market, budget/timeline. | `pitch_resource_monitor`. | Treating pitch advice as permission to send, no playable build, no ask, generic survival pitch. | `https://www.tinybuild.com/how-to-pitch-your-game`, `https://www.tinybuild.com/` |
| MobyGames | Factual database preservation route after public official facts exist. | Public game page, credits, release/platform facts, screenshots/media rights, duplicate check. | `technical_database_update`. | Marketing adjectives, unsourced credits/platform/release, unreleased private facts, duplicate entry. | `https://www.mobygames.com/info/contribute/`, `https://www.mobygames.com/info/faq3/` |
| Giant Bomb wiki | Factual wiki/database route after public facts exist and original text can be written. | Public official facts, game page/source, screenshots/media rights, duplicate check, original neutral text. | `technical_database_update`. | Copying press release text, promotional wording, unsourced platforms/release, no public source. | `https://www.giantbomb.com/game/create/`, `https://www.giantbomb.com/forums/delete-combine-requests-34/wiki-rules-faq-1466069/` |
| RAWG | Database/API monitor/listing route; do not use as public demand proof. | Public official facts, store links, media, release/platform facts, API attribution if used. | `database_listing`. | Treating API/discovery listing as traction, unsourced fields, no attribution where API data is used. | `https://rawg.io/`, `https://rawg.io/apidocs` |
| SteamGridDB | Artwork catalog route only after public app/game identity and rights-clean capsule/grid assets exist. | Public game identity, Steam/app/source link, rights-clean grid/hero/logo assets, upload owner. | `artwork_database_hold`. | Uploading unofficial/AI/borrowed art, no public app/source, asset conflicts with Steam capsule truth. | `https://www.steamgriddb.com/`, `https://www.steamgriddb.com/help`, `https://www.steamgriddb.com/faq` |

### V9 Priority

1. `REGIONAL_STORES_ARE_RELEASE_ROUTES`: VK Play, WeGame, TapTap, and similar routes need legal/payment/compliance/localization/support proof, not curiosity.
2. `RU_MEDIA_IS_NOT_FREE_REACH`: Pikabu, vc.ru, PlayGround, Kanobu, and StopGame need RU owner coverage, news/technical value, disclosure, and paid/editorial separation.
3. `RESELLERS_NEED_TERMS`: GMG-style routes require pricing/key/support/discount policy before any partner conversation.
4. `DATABASES_ARE_FACTUAL_ONLY`: MobyGames, Giant Bomb, RAWG, SteamGridDB, PCGamingWiki, IGDB, Fandom, and wiki.gg cannot carry adjectives, future plans, or unsourced platform/release claims.
5. `PITCH_ADVICE_IS_NOT_PERMISSION`: tinyBuild-style resources can improve pitch packets; they do not authorize publisher sends without build, deck, ask, and owner.

## Imageboard / Chan Strategy Addendum

Evidence boundary: 4chan, Dvach, and similar anonymous imageboards are volatile community-sentiment surfaces. They are useful for harsh readability critique and language mining. They are not statistical market proof, not creator CRM, not permission to post links, and not a safe place for official announcements.

### Prime Chan Rule

Use imageboards as a pressure test for proof assets, not as acquisition channels.

Do not:

- astroturf;
- pretend to be a player;
- use sockpuppets;
- bump your own thread;
- drop a Steam/Discord/wishlist link unless same-day board rules and HECTON permission gates allow it;
- turn anonymous comments into outreach contacts;
- argue with critics;
- present AI-agent usage as a selling point;
- attack Subnautica, Subnautica 2, Unknown Worlds, Krafton, Unity, Unreal, Godot, or their players;
- use "Subnautica killer", "AI-made", "generated game", "we fixed what they failed", or similar bait.

Do:

- identify as developer if posting as HECTON;
- post only real in-game screenshot or gameplay capture;
- ask one narrow critique question;
- keep the first post no-link by default;
- record board, thread, timestamp, media shown, critique buckets, and action taken;
- stop replying when the thread turns into flame, politics, harassment, or AI-slop bait.

### Board Fit Map

| Surface | Use | Main question | Default action | Risk |
|---|---|---|---|---|
| 4chan /vg/ agdg-style game-dev threads | WIP proof critique from other devs. | Does the mechanic/readability survive hostile viewing? | No-link media + exact critique ask. | High bluntness; low patience for sales copy. |
| 4chan /g/ game-engine/dev threads | Technical/workflow critique only. | Is the rendering/tooling/fake-first approach defensible? | Mention constraints and ask for failure cases. | Engine wars and low signal if framed as promo. |
| 4chan /g/ AI/vibe-coding threads | Internal workflow listening. | What guardrails do devs trust or mock? | Monitor only unless discussing process, not HECTON marketing. | AI-slop association can poison game perception. |
| 4chan /v/ | General player sentiment around underwater/survival games. | What comparison language appears organically? | Monitor only. | Very high backlash risk for direct marketing. |
| Dvach /gd/ | RU gamedev progress and critique. | Does the frame/mechanic read without explanation? | Developer-labeled no-link critique post if asset is ready. | Small community; repeated posting becomes noise fast. |
| Dvach /v/ or /vg/ | RU player language and competitor expectations. | What players compare HECTON to? | Monitor only by default. | General videogame boards punish promotion. |
| Dvach /ai/ | RU AI-agent/workflow sentiment. | Which agent workflows are trusted or ridiculed? | Monitor for internal operations only. | Not a player-acquisition channel. |

### Phase Strategy

| Phase | Chan action | Required proof | Forbidden move |
|---|---|---|---|
| Pre-screenshot | Monitor only. Mine wording, clone-risk, AI-agent skepticism, Unity/Godot/UE sentiment, and underwater-survival comparison language. | Source row with URL/thread/date and confidence. | Posting concept art, lore, mood boards, or Steam copy. |
| First screenshot | One critique drop in a dev-friendly thread only. | Real in-game capture, no performance claim, one readable player/system decision or explicit identity-read question. | Store link, wishlist ask, "rate my game", AI-art-looking key visual. |
| First 10-20 sec clip | Ask whether the action reads without caption. | Clip with visible instrument/machine/failure/decision. | Trailer-style montage with no player verb. |
| Demo/private test exists | Ask for specific failure cases and first-loop confusion. | Build/demo route proof and permission gates outside the board. | Key giveaways, private invites, or Discord recruitment from the thread. |
| Post-demo digest | Convert repeated critique into asset/product notes. | Classified signal table. | Replying to every insult or defending scope. |

### Go / No-Go Gate For Any Imageboard Post

GO only if all are true:

- same-day board/thread rules checked;
- real media exists;
- the post is developer-honest;
- no public CTA is required;
- one narrow question is named;
- response owner is available for 24 hours;
- no co-op, performance, world-size, demo, Steam, or wishlist claim is present unless the exact HECTON gate already allows it;
- AI tools are not used as the hook.

NO-GO if any are true:

- asset is placeholder, lore-only, key-art-only, or AI-looking;
- screenshot needs a paragraph to explain the mechanic;
- copy names competitor pain as the hook;
- post would require fake-user tone;
- there is no plan to classify the feedback;
- expected output is "awareness" rather than critique.

### Best Content Angles For Chans

Use concrete, inspectable proof:

- instrument begins lying or desyncing from the scene;
- machine saves the player, then creates a new cost;
- pressure/flood/power state changes with visible affordance;
- sonar warns before visual contact;
- route choice is visible: retreat, seal, repair, reroute, salvage, hold position;
- black-water silhouette creates decision pressure, not passive monster posing;
- industrial material language: seals, gauges, ballast, pipes, corrosion, cable slack, service panels.

Avoid weak chan content:

- pure beauty shots;
- cozy base tourism;
- generic coral/reef imagery;
- lore exposition;
- logo/capsule-only drops;
- "made with AI agents" process bragging;
- "we are different from Subnautica" without the image proving it.

### AI-Agent Handling

Current safe posture:

- AI agents are internal production tools, not the product promise.
- Public quality is judged by builds, screenshots, clips, and stability, not by how agents were used.
- If asked, answer that agents help with narrow routine work and documentation, while architecture, build safety, public claims, and final asset approval remain human-owned.

Do not:

- claim agents make development faster unless measured internally;
- show agent transcripts as marketing;
- imply generated art/code is a feature;
- debate "AI will replace devs" inside a HECTON asset thread.

### Signal Capture Template

Use this after every imageboard scan or post:

```text
Date:
Surface:
Board/thread:
URL:
Thread status: live / archived / 404
Media shown:
Prompt asked:
Response count:
Useful critique count:
Hostility class: low / medium / high / unusable
Repeated phrases:
Clone-risk signal:
AI-slop signal:
Unity/engine signal:
Readable player decision named by viewers:
Actionable asset/product change:
Marketing action: keep / revise / kill / monitor only
Confidence: anecdotal / directional / recurring
```

### Kill Switches

Stop engaging immediately if:

- the thread becomes a political, identity, or AI-war argument;
- users ask for private keys, Discord access, or unreleased files;
- criticism is only insults with no asset-specific signal;
- someone tries to pull the developer into competitor attacks;
- the same question has already been answered twice;
- any official account/security route would be exposed.

## Comment Response Rules

If someone says "Subnautica clone":

Good response:

> Fair. That is exactly the risk we are trying to avoid. The intended difference is pressure, machinery, industrial survival, and deep-sea noir instead of bright alien wonder. Which part of the image still reads too close?

Bad response:

> It is totally different, you just do not understand.

If someone asks about co-op:

Good response:

> Current public plan is single-player-first. We are not promising co-op.

Bad response:

> Maybe later, stay tuned.

If someone asks about performance:

Good response:

> Performance language waits for measured public builds.

Bad response:

> It will run great on low-end hardware.
