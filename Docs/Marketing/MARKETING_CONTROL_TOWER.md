# HECTON-8 Marketing Control Tower

Status: primary execution map / anti-sprawl gate
Owner lane: Marketing / marketing command
Runtime impact: none
Current phase: pre-screenshot / no public push

## Why This Exists

The marketing folder is now broad enough. More documents are not progress unless they reduce execution risk. This file is the control layer: read it first, then open only the specific operational document needed for the task.

Current hard truth: HECTON-8 has marketing preparation, but no public screenshot pack, no Steam page, no demo, no measured performance proof, and no public conversion data. Therefore the correct work now is preparation, verification, asset criteria, and proof gates. Not hype.

## 2026-05-26 External Reality Update V7

Subnautica 2 is no longer a future trailer target. Steam lists it as released on 2026-05-14, with single-player, online co-op, cross-platform multiplayer, a large Very Positive review base, 11 supported languages, and a public Early Access plan that expects years of updates. V7 official-platform snapshot from 2026-05-26 records Steam review API 89,716 positive / 8,629 negative / 98,345 all-language reviews and 55,738 positive / 3,938 negative / 59,676 English reviews, both `Very Positive`, plus 90,944 appdetails recommendations. Snapshot source: `https://store.steampowered.com/app/1962700/Subnautica_2/`.

PC Gamer and other launch coverage report millions of copies sold in the first launch window and hundreds of thousands of concurrent players. Implication: do not build the marketing plan around SN2 collapse. Their launch momentum is real. HECTON-8 must win through a sharper promise: single-player pressure, machinery, salvage, Seed Ship anomaly, grim industrial identity, and proof-backed performance once we can measure it.

Official Steam screenshots checked on 2026-05-26 show bright/cozy alien ocean, clean modular bases and interiors, readable co-op/player presence, soft rounded vehicles, and one stronger orange biome. They do not own industrial pressure-vessel dread, corrosion, dirty machinery, base failure, or black-water route risk. HECTON's competitive ceiling is therefore not "prettier Subnautica"; it is a harsher single-player pressure contract proven by assets, not copy.

Launch-week weak signals to monitor, not publicly attack:

- EULA/privacy/content-creator concern articles and negative-review clusters.
- Anecdotal co-op desync reports around shared world state, growbeds, storage, and building.
- Anecdotal movement/stutter reports, including input-repeat and traversal-area stutter.
- Base-building friction where flexible construction makes some players miss readable prefabs.
- Regional review split, especially non-English outliers on Steam.

These are research leads only. They are not public ammunition and not proof that HECTON-8 is better.

## Anti-Sprawl Rule

Do not create a new marketing document unless all are true:

1. no existing document can hold the information;
2. the new document has an owner and a decision it controls;
3. it removes operational ambiguity;
4. it is linked from this file or `README.md`;
5. it does not duplicate a table/template that already exists.

Default action: update an existing document or tracker.

## Current Single Source Set

If an agent has no context, read only these first:

| Purpose | File |
|---|---|
| Entry point | `MARKETING_CONTROL_TOWER.md` |
| Directory index | `README.md` |
| Multiplayer-scope boundary | `NO_COOP_PUBLIC_POSITIONING.md` |
| Public identity | `BRAND_AND_POSITIONING_BIBLE.md` |
| Backlog | `Data/MARKETING_BACKLOG_INDEX.md` |
| Source truth | `Data/SOURCE_LEDGER.md` |
| Risk truth | `Data/MARKETING_RISK_REGISTER.md` |

Everything else is opened only when doing that lane's work.

## Current Operating State - 2026-05-21

| Area | Current state | Next valid action |
|---|---|---|
| CRM | 100 staged creator rows, 0 raw. Distribution: 23 `VERIFY_BEFORE_CONTACT`, 22 `NEEDS_ASSET`, 52 `LOW_PRIORITY_VERIFY_LATER`, 3 `DO_NOT_CONTACT`. Send-log fields exist, including `asset_ids_sent`, `creator_utility_score`, `send_route_class`, and `reply_consent_provenance`; paid creator rows now carry `paid_creator_permission_gate`, and all current rows are blocked. No row is contacted. | Do not scrape more until Wave A can be matched to real assets, utility 3/4+, verified contact routes, and explicit send/reply route fields. Do not pay creators unless the CRM row is `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED`. |
| Assets | 13 planned asset slots exist: 8 screenshots, 4 clips, 1 capsule rough. All current slots have `creator_send_gate = BLOCKED_PLANNED_CAPTURE`; metadata also has `multiplayer_scope_check`, `performance_claim_check`, `feature_truth_check`, `pain_bucket_answered`, `pain_proof_score`, `pain_freshness_source`, `pain_freshness_checked_at`, `public_comparison_gate`, `agency_decision_proof_gate`, `agency_decision_notes`, `capture_handoff_packet_id`, `capture_verdict`, `viewer_named_decision`, and `capture_next_actions`. | Capture real assets and produce the first-capture handoff packet: file paths, build ID, QA score, reject codes, creator rows/utility/send gate, pain-proof/freshness, public comparison gate, agency proof gate/notes, packet ID, viewer-named decision, verdict, and capped next actions. First packet must include identity, player verb, base/machinery, and one agency/decision proof asset (`PLAN-SHOT-006`, `PLAN-CLIP-001`, or `PLAN-CLIP-003`); `AGENCY_MISSING_HOLD` keeps Campaign 01 held. |
| Steam | Pre-capture page assembly, launch gate, Official CTA/contact preflight, and Steam announcement/news pipeline exist. Page publication is held by `steam_page_publish_permission_gate = HOLD_NO_STEAM_PAGE_PUBLICATION`; public links are held by `public_cta_permission_gate = HOLD_NO_PUBLIC_CTA`; Steam news/events are held by `steam_announcement_permission_gate = HOLD_NO_STEAM_ANNOUNCEMENT`. | Do not launch page before Campaign 01 `KEEP`, asset intake/QA, `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED` for the exact app/page, `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` for the exact public link, `steam_announcement_permission_gate = ALLOW_STEAM_ANNOUNCEMENT_VERIFIED` for any Steam announcement, official Steam URL custody, inbox custody, and presskit/contact state are clear. |
| Spend | Current recommendation is 0 USD. Paid microtest rows carry `spend_permission_gate`; paid creator CRM rows carry `paid_creator_permission_gate`; all current spend gates are blocked. No budget tier, PMT ID, rate-card reply, or creator name is spend permission. | Release only 0-50 USD cold-read help after real assets exist; no paid ad microtest unless the selected row is `spend_permission_gate = ALLOW_PAID_MICROTEST_VERIFIED`, and no paid creator test unless the selected CRM row is `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED`. |
| Social | Handle checks are candidate-only; no accounts registered by agent. `account_registration_permission_gate = HOLD_ACCOUNT_CREATION`; `public_post_permission_gate = HOLD_NO_PUBLIC_POST`; personal browser sessions/cookies/chat permission are not custody proof. | Human owner records project email, password manager vault item, recovery, 2FA, backup-code custody, approved handle, approved profile assets, and vault URL destination; register only after `ALLOW_ACCOUNT_REGISTRATION_VERIFIED`, and post only after `ALLOW_PUBLIC_POST_VERIFIED`. |
| Official inbox | No owner-controlled project inbox is recorded in marketing docs. `official_inbox_custody_gate = HOLD_NO_PROJECT_INBOX_CUSTODY`. | Create/record the inbox through the Official Project Inbox Gate before social registration, presskit contact, creator access, keys, paid creator deals, or support routing; use it only after `ALLOW_OFFICIAL_INBOX_USE_VERIFIED`. |
| Press/curators | 30 press rows and 20 curator rows triaged. Tracker `status` values are triage labels, not send permission; CSVs now carry `send_permission_gate`, and all current rows are blocked or do-not-contact until named artifacts, same-day route check, official inbox, `send_route_class`, and reply-provenance rules pass. Public press releases, media one-pagers, and presskit-live announcements are held by `press_release_permission_gate = HOLD_NO_PRESS_RELEASE_PUBLICATION`. | Recheck exact routes only after presskit/Steam page/assets exist; do not send unless press rows are explicitly `ALLOW_PRESS_SEND_VERIFIED` or curator rows are `ALLOW_CURATOR_SEND_VERIFIED`. Do not publish/reuse press release copy unless the exact surface has `ALLOW_PRESS_RELEASE_PUBLISH_VERIFIED` plus CTA/Steam/post/localization/access/support gates where relevant. |
| Showcases/festivals | 8 showcase rows tracked, including Steam Next Fest as `SHOW-001`. Tracker `status` values are monitor/not-ready labels; CSV now carries `submission_permission_gate`, and all current rows are blocked. | Do not submit, register, commit, claim participation, or reserve the Next Fest beat unless a row is explicitly `ALLOW_SHOWCASE_SUBMIT_VERIFIED` after same-day rules/deadline check, fee/ROI or one-shot opportunity decision, asset pack, Steam/CTA or private-review route custody, agency-proof fields, owner, and measurement route pass. |
| Community | FAQ, crisis, Reddit, Steam forum/review gates exist. Discord is draft-only with `discord_open_permission_gate = HOLD_NO_DISCORD_PUBLIC_OPEN`; Steam support/forum/review response is held by `steam_support_permission_gate = HOLD_NO_STEAM_SUPPORT_PUBLIC_ROUTE`. No public invite, server announcement, member count, pinned Steam thread, review reply, support route, or public Discord CTA is active. | Do not open noisy community surfaces before proof assets. Public Discord requires server-specific `ALLOW_DISCORD_OPEN_VERIFIED`; Steam support/forums/replies require surface-specific `ALLOW_STEAM_SUPPORT_ROUTE_VERIFIED`, Steam/admin custody, support owner, pinned packet, known-issues/build state, route provenance, and CTA/Discord/private-access gates where linked. |
| Owned audience | Signup/list/email work is draft-only. `owned_audience_permission_gate = HOLD_NO_OWNED_AUDIENCE`; no list import, signup push, or newsletter send is active. | Prepare copy only; do not publish signup forms or send list email until the exact mode has `ALLOW_OWNED_AUDIENCE_VERIFIED`, provider/inbox custody, unsubscribe/delete, consent provenance, route class, and public/private route gates. |
| Regional/localization | Regional copy is draft-only. `localization_public_permission_gate = HOLD_LOCALIZED_PUBLIC_USE`; RU/DE/PT-BR/ES/FR/PL/JP/KR drafts and regional lead rows are not send-ready. | Use localized copy only after language/surface-specific `ALLOW_LOCALIZED_PUBLIC_USE_VERIFIED`, native/fluent review, encoding clean pass, English proof gates, route-specific send/CTA/access/post gates, and provenance fields pass. |
| Measurement/reporting | KPI and analytics tables require first-capture handoff fields for asset packets, `route_class` / `consent_provenance` for feedback/forms/support/public links, `send_route_class` / `reply_consent_provenance` for creator/press/curator replies, permission gate/source for reportable rows, and exact private access-log fields for private access replies: `verified_contact_route`, `access_route_class`, `reply_status_after_send`, `reply_consent_provenance`, plus `agency_decision_field_source` where proof claims are used. Agency proof rows also require `what_decision_next`, `agency_decision_read`, `agency_decision_read_comments`, or `cold_read_agency_decision` as applicable. Public CTA activation uses `public_cta_permission_gate`; private access uses access logs. | Do not report signal from a row that lacks first-capture handoff fields, route/source permission fields, or claims agency proof without the decision-read field; `unknown` route/provenance values are quarantine only. |
| Monitoring | SN2 is strong; V7 public Steam API/appdetails/screenshot sample maps pain buckets and visual gaps to planned HECTON proof assets. Pain signals remain internal research only. | Recheck on the same day before first HECTON screenshot drop; use pain buckets to prioritize capture only through `pain_freshness_source`, `pain_freshness_checked_at`, `viewer_named_decision`, and `capture_verdict`, not public attack copy. |
| Promise/copy | Promise Lint Gate exists. | Any public sentence must be tagged and proof-checked before use. |
| Site/presskit | No-link holding state and presskit minimums exist. Public presskit announcement/release copy is held by `press_release_permission_gate = HOLD_NO_PRESS_RELEASE_PUBLICATION`. | Do not publish more than a minimal holding page before official contact and real assets. Do not announce or link a public presskit, media one-pager, or press release unless `press_release_permission_gate = ALLOW_PRESS_RELEASE_PUBLISH_VERIFIED` and every public link has destination-specific CTA custody. |
| Launch/demo ops | War-room dry run, demo access scoring, key/access compliance, and playtest route custody gates exist. Public demo/Playtest access is held by `demo_public_access_permission_gate = HOLD_NO_PUBLIC_DEMO_ACCESS`; private access is held by `private_access_permission_gate = HOLD_NO_PRIVATE_ACCESS`. | No public demo/Playtest access without `demo_public_access_permission_gate = ALLOW_PUBLIC_DEMO_ACCESS_VERIFIED`; no private demo/key/playtest/preview/Curator Connect recipient or batch access without `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`; no launch/demo/key/playtest batch without named owners, clean links, AB-009/KPI field source for gameplay/pressure/route-risk claims, exact access-log fields for private routes, route/consent fields for public routes, and stop rules. |

## Current Gates

| Gate | Required proof | Allowed marketing |
|---|---|---|
| G0 - Now | No public assets yet. | Lead verification, copy prep, asset criteria, monitoring, internal tests. |
| G1 - Screenshot Pack | 6-10 real in-game screenshots score 10/12; creator-facing assets score utility 3/4+ and map to named CRM rows; first public testing includes one agency/decision proof asset from `PLAN-SHOT-006`, `PLAN-CLIP-001`, or `PLAN-CLIP-003`; AB-009/KPI rows must record the viewer-named decision. | Critique posts, small creator warmup, Steam page drafting. |
| G2 - Steam Page | Public Coming Soon page, capsule, tags, UTM, Official CTA/contact preflight. | Wishlist CTA, tracked creator batches, press fit-check only after CTA/contact state is factual. |
| G3 - Demo/Playtest | Stable first route, known issues, feedback form, and access log with `verified_contact_route`, `access_route_class`, `reply_status_after_send`, `reply_consent_provenance`, and `agency_decision_field_source` for gameplay/pressure/route-risk access copy. | Demo outreach, Steam Playtest, broader creator/press. |
| G4 - Launch/EA | Price, build, support, reviews, war room. | Launch campaign, paid tests if baseline works. |

Current gate: `G0`.

## Absolute Public Boundaries

- Public scope stays single-player-first unless a build proves otherwise.
- Competitor-neutral positioning only.
- Performance claims require measured build/hardware/settings/frame-time.
- No public demo until the first route proves the identity.
- No broad outreach before screenshots or Steam page.
- No paid ads before Steam page conversion baseline.
- No paid ad microtest from PMT status, budget tier, or platform candidate; paid ads require `spend_permission_gate = ALLOW_PAID_MICROTEST_VERIFIED`.
- No paid creator placement from audience fit, rate-card reply, or high-value name; paid creator spend requires `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED`.
- No raw key drops to unverified contacts.
- No key/access/playtest/demo outreach copy can claim gameplay/pressure/route-risk proof without `agency_decision_field_source`, `verified_contact_route`, `access_route_class`, `reply_status_after_send`, and `reply_consent_provenance` custody.
- No fake discovery, astroturfing, bought lists, bought wishlists, or review manipulation.
- No placeholder website that looks like a launch.
- No official account creation, login, posting, following, DMing, or profile publication from personal browser sessions, cookies, remembered passwords, or chat permission alone.
- No official account registration from preflight status, candidate handle, browser state, or chat permission; registration requires `account_registration_permission_gate = ALLOW_ACCOUNT_REGISTRATION_VERIFIED`.
- No official inbox use from address/TBD prose alone; inbox-dependent routes require `official_inbox_custody_gate = ALLOW_OFFICIAL_INBOX_USE_VERIFIED`.
- No public CTA link from page existence, candidate handle, placeholder, private access route, or generic CTA prose; public links require destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`.
- No public Steam Coming Soon/store page, page visibility change, public Steam page launch, public demo/store surface, wishlist campaign claim, or "Steam page is live" signal from asset existence, page draft, Steamworks app shell, candidate URL, CTA planning, Steam announcement approval, press release approval, or wishlist campaign readiness alone; Steam page publication requires `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`.
- No public Steam demo, public demo button, Next Fest demo availability, public Steam Playtest signup/tranche, demo-live claim, or public demo feedback route from build launches, Steam page publication, CTA approval, private access approval, known-issues draft, feedback form, announcement draft, or first-route-playable prose alone; public demo/Playtest access requires `demo_public_access_permission_gate = ALLOW_PUBLIC_DEMO_ACCESS_VERIFIED`.
- No Steam Next Fest registration, commitment, participation claim, or event-beat reservation from Steam page readiness, public demo readiness, CTA approval, Steam announcement approval, Campaign 04 prose, or tracker `status` alone; `SHOW-001` requires `submission_permission_gate = ALLOW_SHOWCASE_SUBMIT_VERIFIED`.
- No private demo/key/playtest/preview/Curator Connect access from build existence, recipient fit, or route prose; private access requires `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`.
- No public post from draft existence, account existence, asset QA score, or no-link route class alone; posts require `public_post_permission_gate = ALLOW_PUBLIC_POST_VERIFIED`.
- No signup form, list import, newsletter send, or signup count from form existence, provider existence, public CTA, or contact list alone; owned audience use requires mode-specific `owned_audience_permission_gate = ALLOW_OWNED_AUDIENCE_VERIFIED`.
- No public Discord server, invite, announcement, CTA, member-count signal, creator/press room, demo-support channel, or regional Discord surface from server draft, channel template, moderator willingness, community interest, or public CTA alone; Discord opening requires server-specific `discord_open_permission_gate = ALLOW_DISCORD_OPEN_VERIFIED`.
- No pinned Steam forum thread, Steam support link, official Steam review/forum reply, support digest, known-issues post, or review-response signal from Steam page existence, demo existence, known-issues draft, public CTA, Discord, or angry thread alone; Steam support use requires surface-specific `steam_support_permission_gate = ALLOW_STEAM_SUPPORT_ROUTE_VERIFIED`.
- No Steam announcement/news/event, scheduled event, patch note, demo reminder, Coming Soon announcement, or Steam news reuse from devlog draft, Steam page existence, demo existence, public post approval, CTA approval, or event template alone; Steam announcements require post-specific `steam_announcement_permission_gate = ALLOW_STEAM_ANNOUNCEMENT_VERIFIED`.
- No press release, public presskit announcement, media one-pager, site presskit block, email press release, Steam-news reuse, social/blog release copy, wire copy, or embargo announcement from template, presskit draft, Steam page existence, public CTA approval, public post approval, press tracker status, or send permission alone; release surfaces require `press_release_permission_gate = ALLOW_PRESS_RELEASE_PUBLISH_VERIFIED` plus surface-specific CTA, Steam announcement, press send, public post, private access, support, and localization gates where used.
- No localized/regional pitch, caption, Steam copy, announcement, one-pager, subtitle, creator message, press note, public post, CTA, or regional signal from encoding repair, owner-native familiarity, draft translation, raw regional lead, or regional interest alone; localized public use requires language/surface-specific `localization_public_permission_gate = ALLOW_LOCALIZED_PUBLIC_USE_VERIFIED`.
- No public sentence that cannot be classified by the Promise Lint Gate.
- No KPI, analytics, or weekly-report signal from feedback/contact/link rows that lack the route-specific class field and `consent_provenance` or `reply_consent_provenance`.
- No KPI, analytics, or weekly-report agency proof from gameplay/pressure/route-risk rows that lack `what_decision_next`, `agency_decision_read`, `agency_decision_read_comments`, or `cold_read_agency_decision`.

## Work Lanes

| Lane | Open only when doing this | Output |
|---|---|---|
| Brand/positioning | `BRAND_AND_POSITIONING_BIBLE.md` | Public one-liners, pillars, forbidden claims. |
| Proof assets | `QA/MARKETING_ASSET_QA_CHECKLIST.md`, `Content/SCREENSHOT_AND_CLIP_SHOTLIST.md`, `Creative/VISUAL_IDENTITY_AND_KEY_ART_DIRECTION.md` | Screenshot/clip pass-fail decisions. |
| Steam page | `Steam/STORE_PAGE_COPY_MATRIX.md`, `SEO/STEAM_TAG_AND_SEARCH_STRATEGY.md`, `Steam/WISHLIST_CONVERSION_AND_PAGE_ITERATION_PLAN.md` | Store copy, tags, screenshot order. |
| Creator outreach | `CreatorOutreach/CREATOR_CRM_SCHEMA_AND_SCORING.md`, `CreatorOutreach/MASS_LEAD_VERIFICATION_AND_PITCH_WORKFLOW.md` | Triaged or verification-gated leads and gated send-later messages. |
| Press/showcases | `Press/PRESS_AND_STEAM_CURATOR_TARGETS.md`, `Press/PRESS_RELEASE_AND_EMAIL_TEMPLATES.md`, `Press/PRESS_KIT_AND_MEDIA_PLAN.md`, `Press/SHOWCASE_AND_FESTIVAL_SUBMISSION_PLAYBOOK.md`, `Press/STEAM_CURATOR_CONNECT_PLAYBOOK.md` | Verified press/showcase/curator targets, press release gate, and presskit publish boundary. |
| Community | `Community/PUBLIC_FAQ_AND_OBJECTION_HANDLING.md`, `Community/REDDIT_COMMUNITY_RULES_TRACKER.md`, `Community/CRISIS_AND_MODERATION_PLAYBOOK.md` | Safe replies, rule checks, crisis handling. |
| Demo/playtest | `Steam/DEMO_PLAYTEST_AND_TELEMETRY_PLAN.md`, `Audience/PLAYTESTER_RECRUITMENT_AND_SCREENING_PLAN.md` | Playtest waves, feedback forms, demo gate, exact private access-log field set, and agency-decision field custody. |
| Measurement | `Analytics/MEASUREMENT_AND_UTM_PLAN.md`, `KPI/MARKETING_DASHBOARD_SPEC.md` | UTM, dashboard, weekly report. |
| Launch | `Launch/LAUNCH_DAY_AND_FIRST_WEEK_WAR_ROOM.md`, `Steam/PRICING_DISCOUNT_AND_EARLY_ACCESS_POLICY.md` | Price memo, launch roles, support loop. |
| Promise/site | `Roadmap/PUBLIC_ROADMAP_LANGUAGE_AND_PROMISE_POLICY.md`, `Website/ONE_PAGE_SITE_AND_PRESSKIT_PLAN.md` | Linted public copy, holding page, presskit minimums. |

## What To Do Now

Only these actions make sense before screenshots:

1. Capture or prepare capture for `PLAN-SHOT-*` and `PLAN-CLIP-*`; docs are now waiting on real frames. The minimum first packet is identity, player verb, base/machinery, and one agency/decision proof asset.
2. Fill asset metadata with build ID, source, owner, QA score, `multiplayer_scope_check`, `performance_claim_check`, `feature_truth_check`, creator utility score, creator send gate, pain-proof score, pain freshness source/date, public comparison gate, agency decision proof gate, agency decision notes, and reject code after capture.
3. Run the first screenshot pack through QA before any Steam, creator, social, or press expansion.
4. Use Wave A creator packet only after asset/contact/`public_cta_permission_gate`-or-access-route gates pass and the CRM row records `asset_ids_sent` plus `creator_utility_score`; use paid creator deals only after the CRM row records `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED`.
5. Set up and record the owner-controlled project inbox, then reserve social handles only after `account_registration_permission_gate = ALLOW_ACCOUNT_REGISTRATION_VERIFIED`.
6. Recheck SN2 sentiment before first screenshot drop, but keep all competitor pain internal.
7. If SN2 pain refresh changes capture priority, update the planned asset packet, QA gate, FAQ, source ledger, and risk register in the same pass.
8. Recheck Steam/Reddit/platform rules same day before any post or store upload; Steam page/public demo publication also requires `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`.
9. Maintain the risk register only when a new operational risk appears.
10. Run Promise Lint on any copy that might leave the docs, including bios, holding page text, pitch snippets, and roadmap lines.
11. For any feedback, support, form, public link, creator-reply, or press-reply row, record the route-specific class field plus `consent_provenance` or `reply_consent_provenance` before reporting signal; for private access rows, record the exact access-log field set.
12. For any agency-proof cold read, first-public beat, creator, or campaign row, record the viewer-named decision in the AB-009/KPI fields before reporting signal.
13. For any key, private preview, Steam Playtest, tester recruitment, or demo outreach copy, remove pressure/route-risk agency claims unless AB-009/KPI field source and route-specific public/private provenance fields are known.
14. Keep public press release/presskit/media-one-pager copy held until `press_release_permission_gate = ALLOW_PRESS_RELEASE_PUBLISH_VERIFIED`; targeted press sends still require press tracker `send_permission_gate`.
15. Keep Steam page publication held until `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`; external links, announcements, posts, release copy, spend, and submissions still require their separate gates. Steam Next Fest also requires `SHOW-001` to have `submission_permission_gate = ALLOW_SHOWCASE_SUBMIT_VERIFIED`.
16. Keep public demo/Steam Playtest access held until `demo_public_access_permission_gate = ALLOW_PUBLIC_DEMO_ACCESS_VERIFIED`; private recipient/batch access still requires `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`.
17. Update this control tower only when current gate, proof state, or top priority changes.

## What Not To Do Now

- Do not create more strategy docs.
- Do not add new creator lists unless they come from a reproducible source and feed an existing tracker.
- Do not write more pitch variants unless tied to a verified lead or asset.
- Do not contact press.
- Do not publish press releases or presskit-live announcements.
- Do not submit festivals/showcases.
- Do not open Discord publicly unless proof assets exist.
- Do not run paid ads.
- Do not treat a PMT row, budget ladder step, or platform candidate as paid-spend approval.
- Do not publish roadmap.

## Execution Rule For Agents

Every marketing task must end with one of these outputs:

- verified row;
- scored asset;
- revised copy;
- source-ledger update;
- risk-register update;
- tracker row;
- campaign decision: proceed / revise / kill.

Generic "research completed" is not enough.

Daily work must now follow `Operations/DAILY_AGENT_TASK_LOOP.md` and end as `ADVANCE`, `HOLD`, or `KILL`.

After any Marketing docs/data edit, run `Operations/DAILY_AGENT_TASK_LOOP.md` End-Of-Change Validation Cut V1. Include the Backtick Path Audit and rationale-order audit when entry, backlog, source-ledger, campaign, presskit, operation, status, or rationale files changed.

## Current Top Priorities

| Priority | Work | File to update |
|---:|---|---|
| 1 | Produce the first real screenshot/clip packet into existing planned asset IDs and fill the first-capture handoff packet, asset-side claim checks, creator utility/send gate, pain freshness, public comparison gate, agency-proof fields, `capture_handoff_packet_id`, `capture_verdict`, `viewer_named_decision`, and `capture_next_actions`; include one agency/decision proof asset before Campaign 01 can advance. | `Content/SCREENSHOT_AND_CLIP_SHOTLIST.md`, `Data/MARKETING_ASSET_METADATA_TEMPLATE.csv`, `QA/MARKETING_ASSET_QA_CHECKLIST.md` |
| 2 | Score first screenshot pack and decide `KEEP`, `REVISE`, or `KILL`. | `Campaigns/CAMPAIGN_01_FIRST_SCREENSHOT_DROP.md` |
| 3 | Assemble Steam page only if screenshot pack earns `KEEP`; publish only if `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`, and point public links only if `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`. | `Steam/STORE_PAGE_COPY_MATRIX.md`, `Campaigns/CAMPAIGN_02_STEAM_PAGE_LAUNCH.md`, `Steam/STEAM_PAGE_ASSET_REQUIREMENTS_CHECKLIST.md` |
| 4 | Test capsule roughs from approved source assets before spending. | `Creative/VISUAL_IDENTITY_AND_KEY_ART_DIRECTION.md`, `Creative/CAPSULE_TRAILER_THUMBNAIL_BRIEFS.md` |
| 5 | Prepare first creator Wave A only after official contact, asset QA, creator utility, and CRM send-log gates pass. | `CreatorOutreach/MASS_LEAD_VERIFICATION_AND_PITCH_WORKFLOW.md`, `Data/CREATOR_VERIFICATION_TEMPLATE.csv` |
| 6 | Keep first-public incident and Steam forum gates ready. | `Community/CRISIS_AND_MODERATION_PLAYBOOK.md`, `Feedback/STEAM_REVIEWS_FORUMS_AND_SUPPORT_RESPONSE_PLAYBOOK.md` |
| 7 | Keep KPI/analytics/weekly-report rows route-classed, `consent_provenance` / `reply_consent_provenance` labeled, and agency-decision-read labeled before counting signal. | `Analytics/MEASUREMENT_AND_UTM_PLAN.md`, `KPI/MARKETING_DASHBOARD_SPEC.md`, `Operations/DAILY_AGENT_TASK_LOOP.md` |
| 8 | Maintain multiplayer-scope/performance/competitor-neutral gates and keep SN2 pain mining private/evidence-labeled. | `NO_COOP_PUBLIC_POSITIONING.md`, `Data/MARKETING_RISK_REGISTER.md`, `Monitoring/COMPETITOR_AND_SENTIMENT_MONITORING_QUERIES.md` |
| 9 | Lint any future public copy and keep site/presskit as holding-only until real assets exist. | `Roadmap/PUBLIC_ROADMAP_LANGUAGE_AND_PROMISE_POLICY.md`, `Website/ONE_PAGE_SITE_AND_PRESSKIT_PLAN.md` |

## Stop Condition

Stop expanding docs. Resume new document creation only after one of these real artifacts exists:

- first screenshot pack;
- Steam page draft with actual capsule/screenshots;
- playable first route;
- demo/playtest build;
- measurable campaign data.

Until then, work inside the existing files.
