# HECTON-8 Marketing Preparation Index

## Authority Boundary

Static documentation only. Current source, active architecture contracts, fresh proof artifacts, and official platform rules override dated claims in this file. No runtime, profiler, memory, render, platform, public-page, or ship-readiness proof is implied by this file alone.

Status: working strategy / pre-screenshot / single-player-first
Owner lane: Marketing / competitive intelligence / marketing preparation
Runtime impact: none

## Read First

Use `MARKETING_CONTROL_TOWER.md` as the operating entry point. This folder is large enough; new marketing documents are forbidden unless the control tower's anti-sprawl rule is satisfied. Default action is to update existing docs, trackers, or templates.

## R18 Platform Actuality Notes

- Steam Next Fest rules are external platform rules. R18 checked the Steamworks Next Fest docs on 2026-05-18; the actionable boundary is still: one Next Fest per title, unreleased base game, public base-game store page, and public playable demo by event start.
- Early Access copy must sell the current playable state, not future promises. Do not imply guaranteed features, dates, finish state, co-op, or performance without artifacts.
- Recheck official Steamworks docs before spending money, launching the store page, submitting a demo, distributing keys, or scheduling public outreach.

## Hard Rules

- Do not market multiplayer/co-op features. Current HECTON-8 public positioning is single-player-first; any future networking R&D is not public scope.
- Do not say "Subnautica killer". It makes the project look derivative and insecure.
- Do not claim performance without fresh proof: profiler, GC allocation capture, frame-time overlay, build target, hardware.
- Do not spend paid ad money before proof assets exist, and do not run a paid microtest unless the selected PMT row has `spend_permission_gate = ALLOW_PAID_MICROTEST_VERIFIED`.
- Do not contact creators with generic spam. Every pitch must name why their channel fits.
- Do not contact creators from asset existence alone. Creator-facing use requires asset QA, `creator_utility_score` 3/4+, `creator_send_gate`, named CRM row mapping, exact contact route, `send_route_class`, and send-log fields.
- Do not pay creators from audience fit, rate-card reply, or high-value name alone. Paid creator tests require the CRM row to have `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED`.
- Do not send press/curator pitches, offers, keys, or access from tracker `status`; press and curator trackers require `send_permission_gate = ALLOW_PRESS_SEND_VERIFIED` or `ALLOW_CURATOR_SEND_VERIFIED`.
- Do not submit showcases or festivals from `MONITOR` / `NOT_READY`; the showcase tracker requires `submission_permission_gate = ALLOW_SHOWCASE_SUBMIT_VERIFIED`.
- Do not register, commit, announce, or reserve Steam Next Fest from page/demo/CTA readiness alone; `SHOW-001` in the showcase tracker requires `submission_permission_gate = ALLOW_SHOWCASE_SUBMIT_VERIFIED`.
- Do not create or use official accounts from a personal browser session, cookies, remembered passwords, candidate handle, or chat permission alone. Social registration requires `account_registration_permission_gate = ALLOW_ACCOUNT_REGISTRATION_VERIFIED`, project inbox, password manager vault, recovery, 2FA, backup-code custody, approved handle, approved profile assets, vault URL destination, and post-registration custody row.
- Do not use an official inbox, presskit contact, account registration email, key route, creator route, support route, or paid creator route unless `official_inbox_custody_gate = ALLOW_OFFICIAL_INBOX_USE_VERIFIED`.
- Do not publish a public Steam Coming Soon/store page, visibility change, public demo/store surface, wishlist campaign claim, or "Steam page is live" signal from asset existence, page draft, Steamworks app shell, candidate URL, CTA planning, announcement approval, or press release approval alone; exact app/page publication requires `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`.
- Do not expose public Steam demo access, a public demo button, Next Fest demo availability, public Steam Playtest signup/tranche, demo-live claim, or public demo feedback route from build launch, Steam page publication, CTA approval, private access approval, known-issues draft, feedback form, announcement draft, or first-route-playable prose alone; exact public demo/Playtest access requires `demo_public_access_permission_gate = ALLOW_PUBLIC_DEMO_ACCESS_VERIFIED`.
- Do not post a wishlist, signup, Discord, presskit, creator-access, paid-traffic, trailer end-card, bio, email, or showcase public link unless the exact destination has `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`.
- Do not send private demo/key/playtest/preview/Curator Connect access unless the exact recipient or batch has `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`.
- Do not publish public posts from draft existence, account existence, asset QA score, or no-link route class alone; each post requires `public_post_permission_gate = ALLOW_PUBLIC_POST_VERIFIED`.
- Do not publish signup forms, import contacts, send newsletters, or count signup signal unless the exact mode has `owned_audience_permission_gate = ALLOW_OWNED_AUDIENCE_VERIFIED`.
- Do not open a public Discord server, publish an invite, announce a server, or count Discord member signal unless the exact server has `discord_open_permission_gate = ALLOW_DISCORD_OPEN_VERIFIED`.
- Do not create pinned Steam forum threads, publish Steam support links, make official Steam review/forum replies, or count support signal unless the exact app/build/surface has `steam_support_permission_gate = ALLOW_STEAM_SUPPORT_ROUTE_VERIFIED`.
- Do not publish or schedule Steam announcements/news/events from devlog drafts, Steam page existence, demo existence, public post approval, CTA approval, or event templates alone; each Steam event/news post requires `steam_announcement_permission_gate = ALLOW_STEAM_ANNOUNCEMENT_VERIFIED`.
- Do not publish, send, cross-post, wire, or announce a press release, public presskit, media one-pager, site presskit block, Steam-news reuse, email press release, or embargo copy from a template, presskit draft, Steam page existence, public CTA approval, public post approval, press tracker status, or send permission alone; exact release surfaces require `press_release_permission_gate = ALLOW_PRESS_RELEASE_PUBLISH_VERIFIED`.
- Do not send or publish localized/regional copy from encoding repair, owner-native familiarity, draft translation, raw regional leads, or regional interest alone; exact language/surface use requires `localization_public_permission_gate = ALLOW_LOCALIZED_PUBLIC_USE_VERIFIED`.
- Do not advance a public asset from QA score alone. Asset metadata must also fill `multiplayer_scope_check`, `performance_claim_check`, and `feature_truth_check`.
- Do not report feedback, support, signup, public-link, private-access, creator-reply, or press-reply signal unless the row carries the route-specific class field, permission gate/source, plus `consent_provenance` or `reply_consent_provenance`.
- Do not use key/access/playtest/demo outreach copy to claim gameplay, pressure, route-risk, threat, salvage, or base-failure proof unless AB-009/KPI has `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`.
- Do not buy fake wishlists, Discord members, views, comments, reviews, curator posts, or key-reseller access.
- Run Promise Lint before any public sentence leaves the docs: public copy must be current-build proof, active-work proof, planned focus, investigating, not planned, or removed.
- After any Marketing docs/data edit, run `Operations/DAILY_AGENT_TASK_LOOP.md` End-Of-Change Validation Cut V1. Include the Backtick Path Audit and rationale-order audit when entry, backlog, source-ledger, campaign, presskit, operation, status, or rationale files changed.

## Directory Map

| Path | Purpose |
|---|---|
| `MARKETING_CONTROL_TOWER.md` | Primary execution map, gate model, anti-sprawl rule, and current priority list. |
| `PREP_DIRECTIONS_NOW.md` | Concrete setup directions that can be done before screenshots exist. |
| `MARKETING_PREP_MASTER_PLAN.md` | Full low-budget plan: now, first screenshots, first gameplay, demo, Next Fest, Early Access. |
| `OUTREACH_CALENDAR_AND_BATCH_PLAN.md` | Phase gates, outreach batch sizes, cadence, and human approval checks. |
| `KEYS_AND_CREATOR_COMPLIANCE.md` | Key policy, anti-scam checks, disclosure requirements, AB-009 proof source boundary, and key log schema. |
| `NO_COOP_PUBLIC_POSITIONING.md` | Corrects agent hallucinations around 100km co-op and locks public language. |
| `BRAND_AND_POSITIONING_BIBLE.md` | Core public positioning, voice rules, differentiation, forbidden claims, and asset identity rules. |
| `Analytics/MEASUREMENT_AND_UTM_PLAN.md` | UTM naming, `public_cta_permission_gate`, funnel tables, route class/permission/consent fields, weekly reports, and metric trust rules. |
| `Ads/PAID_MICROTESTS_AND_AD_CREATIVE_MATRIX.md` | Paid ad gates, `spend_permission_gate`, budget tiers, creative families, test template, and stop rules. |
| `Audience/OWNED_AUDIENCE_EMAIL_AND_NEWSLETTER_PLAN.md` | Email/newsletter list purpose, `owned_audience_permission_gate`, segments, signup offers, templates, cadence, and metrics. |
| `Audience/PLAYTESTER_RECRUITMENT_AND_SCREENING_PLAN.md` | Playtester types, recruitment sources, consent/route gates, screening questions, wave plan, and feedback form. |
| `Budget/LOW_BUDGET_SPEND_DECISION_TREE.md` | Spend gates for 1000/3000/5000 USD budget scenarios, PMT `spend_permission_gate`, and stop rules. |
| `Creative/CAPSULE_TRAILER_THUMBNAIL_BRIEFS.md` | Creative briefs for Steam capsules, trailer structure, thumbnails, and screenshot scoring. |
| `Creative/VISUAL_IDENTITY_AND_KEY_ART_DIRECTION.md` | Visual identity, palette, key-art concepts, capsule rules, screenshot standards, and logo direction. |
| `SEO/STEAM_TAG_AND_SEARCH_STRATEGY.md` | Steam tag order, search keywords, forbidden tag claims, and tag drift monitoring. |
| `Experiments/A_B_TESTING_AND_CREATIVE_EXPERIMENTS.md` | Hypothesis-driven creative tests, sample thresholds, stop rules, and experiment log template. |
| `QA/MARKETING_ASSET_QA_CHECKLIST.md` | Mandatory screenshot, clip, capsule, trailer, post, pitch, presskit, localization, and creator utility QA gates. |
| `Schedule/90_DAY_MARKETING_OPERATIONS_CALENDAR.md` | Week-by-week operating calendar from pre-screenshot setup to demo/Next Fest readiness. |
| `Operations/DAILY_AGENT_TASK_LOOP.md` | Daily agent roles, quotas, verification loop, pitch loop, source audit loop, creator utility fields, and report template. |
| `Monitoring/COMPETITOR_AND_SENTIMENT_MONITORING_QUERIES.md` | Weekly competitor/player-pain search queries, signal taxonomy, and digest template. |
| `Campaigns/CAMPAIGN_00_PRE_SCREENSHOT_SETUP.md` | What to do now before screenshots exist. |
| `Campaigns/CAMPAIGN_01_FIRST_SCREENSHOT_DROP.md` | First screenshot drop plan, critique posts, creator micro-outreach, and metrics. |
| `Campaigns/CAMPAIGN_02_STEAM_PAGE_LAUNCH.md` | Coming Soon page launch sequence, outreach batch, and page conversion kill criteria. |
| `Campaigns/CAMPAIGN_03_FIRST_DEMO_OUTREACH.md` | Demo outreach plan, key policy, creator batches, public/private demo gates, route-specific class / `reply_consent_provenance` gates, and demo metrics. |
| `Campaigns/CAMPAIGN_04_NEXT_FEST_AND_DEMO_EVENT.md` | Steam demo event / Next Fest plan with `SHOW-001` submission gate, `demo_public_access_permission_gate`, and official-rule recheck boundary. |
| `Campaigns/CAMPAIGN_05_REGIONAL_PUSH.md` | Regional creator/press campaign with RU/DE/ES/PT-BR pitch drafts. |
| `Steam/STEAM_WISHLIST_AND_NEXT_FEST_PLAN.md` | Steam page, `steam_page_publish_permission_gate`, wishlist funnel, demo, Next Fest `SHOW-001` submission boundary, Early Access rules and checklist. |
| `Steam/STORE_PAGE_COPY_MATRIX.md` | Steam short descriptions, capsule options, screenshot order, `steam_page_publish_permission_gate`, and cold-reader test. |
| `Steam/STEAM_PAGE_ASSET_REQUIREMENTS_CHECKLIST.md` | Steam asset planning sizes, trailer beats, capsule direction, `steam_page_publish_permission_gate`, and upload QA. |
| `Steam/PRICING_DISCOUNT_AND_EARLY_ACCESS_POLICY.md` | Price bands, discount policy, Early Access gates, regional pricing checks, and price memo template. |
| `Steam/DEMO_PLAYTEST_AND_TELEMETRY_PLAN.md` | Public demo vs Steam Playtest decision, `demo_public_access_permission_gate`, demo scope, telemetry questions, survey, QA gates, `access_route_class` / `reply_consent_provenance`, and agency proof source rules. |
| `Steam/WISHLIST_CONVERSION_AND_PAGE_ITERATION_PLAN.md` | Steam page section order, screenshot order, conversion experiments, and weekly page review. |
| `CreatorOutreach/CREATOR_OUTREACH_DATABASE.md` | Curated public creator lead database and outreach segmentation. |
| `CreatorOutreach/CREATOR_CRM_SCHEMA_AND_SCORING.md` | CRM schema, scoring, status values, verification gates, paid creator permission gate, and send-log fields. |
| `CreatorOutreach/SEGMENT_PITCH_MATRIX.md` | Segment-specific pitch angles, timing, risks, and personalization formula. |
| `CreatorOutreach/PITCH_BANK.md` | Persona pitches, email/DM formats, subject lines, and Russian-language pitch. |
| `CreatorOutreach/A_TIER_PERSONALIZED_PITCHES.md` | Personalized draft angles for top creators, outlets, and regional leads. |
| `CreatorOutreach/MASS_LEAD_VERIFICATION_AND_PITCH_WORKFLOW.md` | How agents turn raw public leads into verified, personalized, asset/utility-gated outreach candidates. |
| `CreatorOutreach/PRIORITY_50_MESSAGE_DRAFTS_FROM_RAW.md` | First 50 raw-signal message drafts; must be verified before sending. |
| `Community/COMMUNITY_POST_TEMPLATES.md` | Reddit, Steam, X/Bluesky, TikTok/Shorts, Discord templates. |
| `Community/COMMUNITY_TARGETS_AND_RULES.md` | Community buckets, post types, candidate spaces, and response rules. |
| `Community/DISCORD_AND_COMMUNITY_SERVER_SETUP.md` | Discord/community `discord_open_permission_gate`, channels, roles, rules, FAQ pins, invite custody, and moderation escalation. |
| `Community/CRISIS_AND_MODERATION_PLAYBOOK.md` | Holding statements and escalation rules for clone, co-op, performance, key-scam, demo, and creator crises. |
| `Community/REDDIT_COMMUNITY_RULES_TRACKER.md` | Reddit/community same-day rule tracker, astroturf firewall, post taxonomy, and candidate spaces. |
| `Community/PUBLIC_FAQ_AND_OBJECTION_HANDLING.md` | Public FAQ and short safe answers for co-op, Subnautica comparison, performance, demo, and objections. |
| `Content/SCREENSHOT_AND_CLIP_SHOTLIST.md` | Capture plan for first screenshots and 20-second gameplay clips. |
| `Content/POST_BANK_AND_HOOK_LIBRARY.md` | Large hook/post/caption/thumbnail bank mapped to screenshot and clip families. |
| `Content/DEVLOG_AND_STEAM_NEWS_PIPELINE.md` | Devlog cadence, `steam_announcement_permission_gate`, templates, first topics, and Steam/news reuse pipeline. |
| `Content/TRAILER_SCRIPT_CAPTURE_AND_EDITING_BRIEF.md` | Trailer length variants, beat sheet, capture rules, text cards, audio direction, and trailer QA. |
| `Feedback/PLAYER_FEEDBACK_TAXONOMY_AND_TRIAGE.md` | Feedback classes, severity, translation of raw comments into product/marketing actions. |
| `Feedback/STEAM_REVIEWS_FORUMS_AND_SUPPORT_RESPONSE_PLAYBOOK.md` | Steam `steam_support_permission_gate`, review response rules, forum templates, known-issues policy, support route custody, and weekly review digest. |
| `Launch/LAUNCH_DAY_AND_FIRST_WEEK_WAR_ROOM.md` | Launch/demo/EA role ownership, dry run, timeline, red alerts, holding statements, and first-week digest. |
| `Legal/COMPLIANCE_AND_DISCLOSURE_PLAYBOOK.md` | Creator disclosure, key distribution, paid placement, and forbidden claim rules. |
| `Localization/LOCALIZATION_AND_REGIONAL_ASSET_PIPELINE.md` | Regional language priority, `localization_public_permission_gate`, asset localization, review gates, and one-pager template. |
| `Monetization/MONETIZATION_RESEARCH_RU_KZ_CRYPTO_2026-05-31.md` | Non-Steam monetization, Russia/Kazakhstan/crypto payment feasibility, account stack, and hold/advance/kill route map. |
| `Partnerships/CREATOR_CONTRACT_TERMS_AND_RATE_CARD.md` | Paid creator deal types, `paid_creator_permission_gate`, rough test ranges, contract checklist, and talking-point boundaries. |
| `Press/PRESS_KIT_AND_MEDIA_PLAN.md` | Press kit shell, factsheet fields, press angles, `press_release_permission_gate`, publish boundary, and embargo/key hygiene. |
| `Press/PRESS_AND_STEAM_CURATOR_TARGETS.md` | Press, newsletters, showcases, YouTube list channels, and Steam curator target map. |
| `Press/PRESS_RELEASE_AND_EMAIL_TEMPLATES.md` | `press_release_permission_gate`, press release skeletons, first reveal/demo emails, follow-up, quote bank, and forbidden lines. |
| `Press/SHOWCASE_AND_FESTIVAL_SUBMISSION_PLAYBOOK.md` | Submission gates, `submission_permission_gate`, asset pack, event-fit scoring, timeline, and kill rules for festivals/showcases. |
| `Press/STEAM_CURATOR_CONNECT_PLAYBOOK.md` | Curator Connect readiness, filtering, `send_permission_gate`, scoring, message templates, and scam-safe response policy. |
| `Press/REVIEW_KEYS_EMBARGO_AND_PREVIEW_ACCESS_PROTOCOL.md` | Review key/access types, `private_access_permission_gate`, press/curator `send_permission_gate`, embargo templates, key log schema, and scam red flags. |
| `Press/PRESS_ANGLE_AND_SUBJECT_LINE_BANK.md` | Press angles, subject lines, outlet-angle map, and proof checklist. |
| `Press/SHOWCASE_SUBMISSION_TRACKER.csv` | Initial tracker for Steam events, PC Gaming Show, Future Games Show, Day of the Devs, The MIX, related targets, and `submission_permission_gate`. |
| `Press/PRESS_TARGET_VERIFICATION_TRACKER.csv` | Press/outlet verification tracker with `send_permission_gate` separate from triage status. |
| `Press/STEAM_CURATOR_CANDIDATE_TRACKER.csv` | Steam curator candidate and discovery-surface tracker with `send_permission_gate` separate from triage status. |
| `Social/SOCIAL_ACCOUNT_SETUP_AND_PLATFORM_PLAYBOOK.md` | Social account priority, handle/bio/pinned templates, `account_registration_permission_gate`, `public_post_permission_gate`, cadence, first posts, and reply rules. |
| `Website/ONE_PAGE_SITE_AND_PRESSKIT_PLAN.md` | One-page site structure, no-link holding state, `official_inbox_custody_gate`, `press_release_permission_gate` for public presskit use, presskit minimums, factsheet, and creator disclosure blocks. |
| `KPI/MARKETING_DASHBOARD_SPEC.md` | Metrics schema, route class/permission/consent fields, targets, and kill criteria for Steam, clips, outreach, and feedback. |
| `Regional/REGIONAL_OUTREACH_PLAN.md` | Regional outreach priorities, localization gate requirements, and first-pass localized pitch drafts. |
| `AgentOps/AGENT_MARKETING_WORKFLOWS.md` | How to use agents as labor: lead mining, verification, copy tests, monitoring. |
| `AgentOps/scrape_letsplayindex_public_leads.ps1` | Hold-by-default public-index lead extraction script; raw CSV refresh requires an explicit source-backed sprint. |
| `AgentOps/generate_priority50_messages.ps1` | Generates first 50 draft messages from the priority creator shortlist. |
| `Operations/ASSET_LIBRARY_NAMING_AND_VERSION_CONTROL.md` | Marketing asset folder structure, filename rules, status values, metadata schema, and rejection codes. |
| `Data/SOURCE_LEDGER.md` | Sources, evidence classes, raw lead-mining notes, and verification rules. |
| `Data/MARKETING_BACKLOG_INDEX.md` | Agent-executable marketing backlog with P0/P1/P2 tasks and spend gates. |
| `Data/MARKETING_RISK_REGISTER.md` | Risk register for multiplayer-scope confusion, clone perception, weak assets, creator utility bypass, key scams, review damage, and launch issues. |
| `Data/CREATOR_VERIFICATION_TEMPLATE.csv` | CSV header/template for turning raw public creator leads into verified CRM rows with structured send-log, `send_route_class`, and `reply_consent_provenance` fields. |
| `Data/MARKETING_ASSET_METADATA_TEMPLATE.csv` | CSV template for public asset build/source/status/hook/QA metadata plus creator utility/send-gate, pain-proof, agency-proof, and first-capture handoff fields. |
| `Data/RAW_PUBLIC_CREATOR_LEADS_README.md` | Data dictionary and verification rules for raw/unique/priority creator CSVs. |
| `Roadmap/PUBLIC_ROADMAP_LANGUAGE_AND_PROMISE_POLICY.md` | Public roadmap promise levels, Promise Lint Gate, forbidden language, safe template, and EA roadmap rules. |

Archived scratch note: old verification batch files were moved to `Docs/DEPRECATED/Active_Doc_Deprecation_2026-05-26/Marketing/AgentOps/VerificationBatches_2026-05-19/`. They are not active CRM, not active input, not a send queue, and not outreach permission.

Archived raw planning sheet: `Docs/DEPRECATED/Active_Doc_Deprecation_2026-05-26/Marketing/CreatorOutreach/PRIORITY_250_PITCH_SHEET_FROM_RAW.md`. It was a parked Top 250 raw public-index pitch sheet, not active CRM and not outreach permission.

Archived raw lead seed queue: `Docs/DEPRECATED/Active_Doc_Deprecation_2026-05-26/Marketing/CreatorOutreach/RAW_LEAD_EXPANSION_QUEUE.md`. It was a public seed list, not verified contacts, not active CRM, and not outreach permission.

Archived raw prospecting lists: `Docs/DEPRECATED/Active_Doc_Deprecation_2026-05-26/Marketing/CreatorOutreach/ADJACENT_SURVIVAL_CREATOR_LEADS.md` and `Docs/DEPRECATED/Active_Doc_Deprecation_2026-05-26/Marketing/Regional/REGIONAL_CREATOR_LEADS.md`. They were raw public prospecting sheets, not active CRM, not verified contacts, and not outreach permission.

Archived raw scrape summary: `Docs/DEPRECATED/Active_Doc_Deprecation_2026-05-26/Marketing/Data/RAW_LEAD_SCRAPE_SUMMARY_2026-05-18.md`. It was a dated human-readable scrape result, not the active CSV schema or current lead workflow.

## Core Position

Competitor positioning note, source-check before public use: the active SN2 route is V7 or newer same-day monitoring only. Subnautica 2 currently presents co-op/multiplayer storefront positioning and owns bright alien-ocean wonder as a market contrast, but this is audience/identity context only. Do not use SN2 co-op momentum, EULA/privacy discourse, performance anecdotes, review negativity, screenshot gaps, or pain buckets as public copy, creator hooks, product superiority proof, or raw-lead expansion reason.

HECTON-8 must own:

- pressure;
- machinery;
- corrosion;
- black water;
- acoustic dread;
- heavy traversal;
- base survival as industrial risk;
- Seed Ship anomaly as systemic threat;
- honest performance receipts when they exist.

One-sentence working pitch:

> HECTON-8 is a single-player-first NASA-punk / deep-sea noir survival game about pressure, salvage, machinery, and the cost of staying alive below the light.

## First Asset Gate

Do not run broad outreach from artifact existence alone. These are minimum proof artifacts, not send/post/link permission:

- 6-10 real in-game screenshots with a consistent identity;
- a 15-20 second gameplay clip that communicates pressure, machinery, and danger without explanation;
- a Steam Coming Soon page draft/page signal with `steam_page_publish_permission_gate` and `public_cta_permission_gate` explicitly held or allowed with source;
- a capture showing no fake performance claims are being made.

First public testing also requires one agency/decision proof asset: `PLAN-SHOT-006`, `PLAN-CLIP-001`, or `PLAN-CLIP-003`. Identity, player verb, and base/machinery shots are not enough if the packet cannot show a readable choice under pressure. The cold-read/dashboard row must preserve the answer in `what_decision_next`, `agency_decision_read`, `agency_decision_read_comments`, or `cold_read_agency_decision`; otherwise it is mood signal, not agency proof.

If a screenshot needs a paragraph to explain why it is good, it is not a marketing asset.

Creator-facing outreach requires more than this first asset gate: the matching asset row must carry creator utility 3/4+, `creator_send_gate` must allow the route, the first-capture handoff fields must not be pending when agency/pressure/route-risk proof is claimed, the CRM row must name the contact route, `send_route_class` and `reply_consent_provenance` must be known, and `asset_ids_sent` plus `creator_utility_score` must be logged before send. Paid creator spend also requires `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED` on the selected CRM row.

Public-facing use also requires the asset metadata row to pass `multiplayer_scope_check`, `performance_claim_check`, and `feature_truth_check`; a strong-looking screenshot cannot bypass claim safety.

Reporting use also requires the route-specific class field, permission gate/source, plus `consent_provenance` or `reply_consent_provenance` where the row came from feedback, support, signup, public CTA, private access, creator reply, or press reply. A useful comment or email cannot become KPI signal if its source permission is unknown. Gameplay/pressure/route-risk rows also need the agency-decision-read fields before they can support a keep/advance decision.

Access use follows the same rule. A key email, private preview invite, Steam Playtest recruitment note, or demo outreach pitch cannot use gameplay/pressure/route-risk proof unless the AB-009/KPI field source is recorded and `access_route_class` / `reply_consent_provenance` are known.

Press, curator, and showcase use also has machine gates. A press or curator row cannot send or receive access unless its `send_permission_gate` is explicitly allowed. A showcase/festival row cannot be submitted unless its `submission_permission_gate` is explicitly allowed. Steam Next Fest is `SHOW-001` in that same tracker; page/demo/CTA readiness does not replace the row gate. Triage `status` values are planning labels only.
