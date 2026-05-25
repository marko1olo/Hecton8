# Campaign 02 - Steam Page Launch

Status: future / requires Steam page assets
Public stance: single-player-first scope / proof-first campaign copy
Runtime impact: none

## Objective

Launch a Coming Soon page that converts cold traffic to wishlists without overselling features. The Steam page is the funnel center; every creator, post, clip, and press beat should point there only after the page can survive cold-read QA.

## Required Before Launch

- Steam capsule readable at thumbnail size.
- 8 real screenshots in correct order.
- short description tested.
- About This Game text cleaned of false roadmap promises.
- tags selected.
- no unsupported multiplayer-scope language.
- performance claims require measured proof.
- press kit shell ready.
- tracking spreadsheet ready.
- `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED` for the exact Steam app/page before public launch.

## Page Position

Short description candidate:

Single-player underwater survival below the light: pressure, salvage, machinery, black-water exploration, and a Seed Ship anomaly that makes the ocean stop feeling neutral.

## Launch Sequence

| Day | Action |
|---:|---|
| -7 | Final cold-read test: capsule + first 4 screenshots + one AB-009 agency/decision candidate. |
| -5 | Verify store copy against forbidden claims. |
| -3 | Prepare Steam announcement, press note, creator micro-pitch; do not schedule the announcement until `steam_announcement_permission_gate = ALLOW_STEAM_ANNOUNCEMENT_VERIFIED`, and do not publish/send/reuse the press note until `press_release_permission_gate = ALLOW_PRESS_RELEASE_PUBLISH_VERIFIED` for the exact surface. |
| -1 | Confirm press kit assets and no broken links; hold public presskit announcement/linking unless `press_release_permission_gate = ALLOW_PRESS_RELEASE_PUBLISH_VERIFIED`. |
| 0 | Publish Coming Soon page only if `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`; post one announcement only if `steam_announcement_permission_gate = ALLOW_STEAM_ANNOUNCEMENT_VERIFIED`, publish/reuse the press release only if `press_release_permission_gate = ALLOW_PRESS_RELEASE_PUBLISH_VERIFIED`, notify first verified creator batch only if first human-send packet gates pass. |
| +1 | Review traffic, wishlists, comments, capsule read. |
| +3 | Send second small creator batch only if page conversion is not terrible and creator send gates still pass. |
| +7 | Publish one systems/dev update, not another generic screenshot dump. |

## Outreach Batch

Do not send to 5000 leads.

First Steam page batch:

- 10 direct underwater survival creators;
- 5 engineering/base systems creators;
- 5 horror/abyss creators;
- 5 regional leads with localized pitch;
- 5 press/newsletter/showcase targets.

Total: 30 sends max.

The Steam page does not open outreach by itself. Every creator notification in this batch must pass first human-send packet gates: asset metadata claim checks, creator utility 3/4+, open `creator_send_gate`, same-day official route verification, Promise Lint, and CRM send-log readiness.

## Pitch

Subject:

HOLD_STEAM_PAGE_LIVE_SUBJECT - use Steam-page-live subject only after `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED` and destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`.

Message:

Hi [Name],

Your channel fits because [verified specific content pattern].

HECTON-8 is a single-player-first underwater survival game about pressure, machinery, salvage, and black-water exploration. Scope stays proof-first and competitor-neutral.

HOLD_STEAM_PAGE_LINK - say the Steam page is live and include the exact link only after `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED` and destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` pass.

The specific angle for your audience is [segment-specific reason].

HOLD_FUTURE_ROUTE_OFFER - mention demo, press kit, build, preview, or material only after exact public CTA, presskit, demo/public access, or recipient/batch private access gates pass and CRM send-log fields are ready.

## Steam Announcement

Title:

HOLD_STEAM_ANNOUNCEMENT_TITLE - use "HECTON-8 Steam Page Is Live" only after `steam_page_publish_permission_gate`, `steam_announcement_permission_gate`, and destination-specific `public_cta_permission_gate` pass.

Body:

HOLD_STEAM_ANNOUNCEMENT_BODY - say HECTON-8 is now on Steam as a Coming Soon page only after app/page publication, Steam announcement permission, and public CTA custody pass.

The current focus is single-player underwater survival: pressure, machinery, salvage, black-water exploration, and habitats that act like survival infrastructure.

What HECTON-8 is not promising:

- unsupported multiplayer scope;
- "Subnautica killer" comparison-war marketing;
- performance claims without measured proof.

Wishlist if the direction fits what you want from darker underwater survival. Feedback on screenshot readability and capsule clarity is still useful.

## Metrics

Track daily:

- visits;
- wishlists;
- wishlist conversion;
- traffic source;
- region;
- screenshot click order;
- comments: "generic", "too dark", "clone", "interesting machinery", "want demo".

Initial useful targets:

- 5-10% visit-to-wishlist for warm traffic;
- 2-5% for cold traffic;
- 30 creator sends -> 3+ replies;
- 1-2 small press/newsletter mentions from the first wave.

## Kill Criteria

Pause outreach and revise page if:

- warm traffic wishlist conversion is below 3%;
- the capsule is repeatedly called unreadable;
- comments ask "what do you do?" after seeing the page;
- multiplayer-scope confusion appears repeatedly;
- players think the screenshots are concept art or renders, not gameplay.

## 2026-05-19 Steam Page Launch Gate V0

Status: blocked until Campaign 01 returns `KEEP`.

Do not launch the Steam page because assets exist. Launch only when the first screenshot campaign proves the page can be understood.

Machine gate:

- current value: `steam_page_publish_permission_gate = HOLD_NO_STEAM_PAGE_PUBLICATION`;
- future allow value: `ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`;
- page drafts, asset QA, Steamworks app shell, CTA packet, announcement draft, or press release approval do not publish the page by themselves.

### Required Upstream Decisions

| Dependency | Required result | Source |
|---|---|---|
| First capture session | `KEEP_TESTING`; no first-page surface depends on `HOLD_ASSET` or `KILL_ANGLE` rows | `Content/SCREENSHOT_AND_CLIP_SHOTLIST.md`, `QA/MARKETING_ASSET_QA_CHECKLIST.md` |
| Screenshot campaign | `KEEP` decision, not `REVISE` or `KILL` | `Campaigns/CAMPAIGN_01_FIRST_SCREENSHOT_DROP.md` |
| Asset intake | Metadata rows for first-page assets have factual path/build/date/source and dashboard join rows | `Operations/ASSET_LIBRARY_NAMING_AND_VERSION_CONTROL.md`, `KPI/MARKETING_DASHBOARD_SPEC.md` |
| Agency/decision proof | `PLAN-SHOT-006`, `PLAN-CLIP-001`, or `PLAN-CLIP-003` passes factual metadata, QA, non-pending `viewer_named_decision`, `capture_verdict = KEEP_TESTING` or stronger campaign `KEEP`, and AB-009/KPI blind-read checks with `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision` filled | `Content/SCREENSHOT_AND_CLIP_SHOTLIST.md`, `QA/MARKETING_ASSET_QA_CHECKLIST.md`, `Steam/STEAM_PAGE_ASSET_REQUIREMENTS_CHECKLIST.md`, `Experiments/A_B_TESTING_AND_CREATIVE_EXPERIMENTS.md`, `KPI/MARKETING_DASHBOARD_SPEC.md` |
| Steam page assembly | Candidate A/B/C selected by evidence | `Steam/STORE_PAGE_COPY_MATRIX.md` |
| Steam asset ticket | 6+ public shots pass and rejects are logged | `Steam/STEAM_PAGE_ASSET_REQUIREMENTS_CHECKLIST.md` |
| Capsule test | AB-002 has a winner | `Experiments/A_B_TESTING_AND_CREATIVE_EXPERIMENTS.md` |
| Cold-read score sheet | AB-001/002/004/009 raw responses logged and thresholds met; gameplay/pressure/route-risk proof preserves the viewer-named decision field | `Experiments/A_B_TESTING_AND_CREATIVE_EXPERIMENTS.md`, `KPI/MARKETING_DASHBOARD_SPEC.md` |
| Tag proof | AB-007 does not create unsupported multiplayer/open-world/sim confusion | `SEO/STEAM_TAG_AND_SEARCH_STRATEGY.md` and experiment log |
| UTM registry | Official Steam URL can be used with canonical names | `Analytics/MEASUREMENT_AND_UTM_PLAN.md` |
| CTA link activation | Destination URL, owner/custody, public state, UTM permission, canonical UTM, and no-link fallback pass | `Analytics/MEASUREMENT_AND_UTM_PLAN.md` |
| Steam page publish gate | `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED` for the exact app/page after Steamworks/admin custody, rule recheck, app URL, owner, rollback owner, page copy, asset packet, and proof gates pass | `Steam/STEAM_PAGE_ASSET_REQUIREMENTS_CHECKLIST.md`, `Steam/STORE_PAGE_COPY_MATRIX.md` |
| Objection handling | FAQ first-screenshot matrix is ready | `Community/PUBLIC_FAQ_AND_OBJECTION_HANDLING.md` |
| Official inbox custody | Project inbox record passes recovery/2FA/backup-code/label requirements | `Website/ONE_PAGE_SITE_AND_PRESSKIT_PLAN.md` |
| Presskit/contact state | Presskit is either minimum viable and linked, or explicitly held out of Steam/creator/press copy | `Press/PRESS_KIT_AND_MEDIA_PLAN.md`, `Website/ONE_PAGE_SITE_AND_PRESSKIT_PLAN.md` |

### Launch Asset Minimum

| Steam surface | Minimum acceptable input |
|---|---|
| Short description | Candidate A unless first assets prove B or C harder. |
| First screenshot | `PLAN-SHOT-001` or winning AB-001 asset. |
| Player verb screenshot | `PLAN-SHOT-003`. |
| Base/machinery screenshot | One of `PLAN-SHOT-002`, `PLAN-SHOT-004`, or `PLAN-SHOT-005`. |
| Agency/decision proof | `PLAN-SHOT-006`, `PLAN-CLIP-001`, or `PLAN-CLIP-003`; `PLAN-SHOT-007` can add anomaly flavor but cannot replace decision proof. The asset metadata row must store non-pending `viewer_named_decision` and a valid `capture_verdict`, and the owning AB-009/KPI row must store the named pressure decision in `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`. |
| Capsule | AB-002 winner from `PLAN-CAPSULE-001`. |
| Trailer | Optional; if included, first 3 seconds must be AB-006 winner. |

### Official CTA And Contact Preflight

Do this before any public announcement, creator notification, press note, or paid test points at the Steam page.

| Item | Required state | Kill if |
|---|---|---|
| Steam page publication | `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED` for the exact app/page. | Page is published from asset existence, page draft, candidate URL, CTA planning, Steam announcement approval, or press release approval alone. |
| Steam URL | Official app URL exists and is copied into UTM registry. | URL is guessed, private, wrong app, or not yet public. |
| Project inbox | `Inbox Custody Record V0` is complete enough for public contact. | Recovery/2FA/backup-code custody is incomplete. |
| Presskit URL | Either live with minimum viable packet or intentionally absent from copy. | Copy says presskit exists but URL/files are missing. |
| Press release / media one-pager | `press_release_permission_gate = ALLOW_PRESS_RELEASE_PUBLISH_VERIFIED` for the exact public or targeted surface; if reused as Steam news, `steam_announcement_permission_gate` also passes; if sent to press, `send_permission_gate = ALLOW_PRESS_SEND_VERIFIED` also passes. | Press release, media one-pager, or presskit-live copy is published/sent from template, presskit draft, Steam page existence, CTA approval, or public post approval alone. |
| Social handles | Either owner-controlled rows exist or announcement omits social cross-links. | Link points to candidate-only or unrelated handle. |
| Creator send route | Wave A remains blocked unless first human-send packet gates pass. | Any creator note sends from Steam launch alone. |
| CTA packet | Analytics CTA activation packet passes for every public link. | Wishlist, signup, Discord, presskit, or creator-access CTA points to a placeholder, candidate-only URL, personal account, or unapproved tracking link. |
| Steam announcement | `steam_announcement_permission_gate = ALLOW_STEAM_ANNOUNCEMENT_VERIFIED` for the exact Coming Soon announcement. | Announcement is scheduled from a draft, public post approval alone, CTA approval alone, or unverified Steamworks/admin/event state. |

### First 7 Days Decision Rule

After launch, choose exactly one path:

- `EXPAND`: warm traffic is at or above 5% visit-to-wishlist, page comments understand the player verb, at least one agency-proof row/comment can name the player decision under pressure, and no major multiplayer-scope/AI/darkness confusion.
- `REVISE_PAGE`: traffic exists but conversion/comments show capsule, screenshot order, or copy problem.
- `STOP_OUTREACH`: warm traffic is below 3%, page is misunderstood, Steam page publish gate/CTA activation/access route fails, or assets are broken.

No press expansion, paid PMT test, or second creator batch before this decision.
