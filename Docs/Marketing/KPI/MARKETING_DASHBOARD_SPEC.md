# Marketing KPI Dashboard Spec

## Authority Boundary

Static documentation only. Current source, active architecture contracts, fresh proof artifacts, and official platform rules override dated claims in this file. No runtime, profiler, memory, render, platform, public-page, or ship-readiness proof is implied by this file alone.

Status: tracking design / no live data yet

## Purpose

This dashboard prevents marketing by vibes.

No metric means no claim. No source means no decision.

R19 benchmark boundary: every target in this document is provisional until replaced by Steam/UTM/outreach/demo telemetry. Default source is `INTERNAL_ASSUMPTION`; do not treat target bands as market forecast, proof, or public copy.

## Core Tables

### Steam Daily

| Field | Type | Notes |
|---|---|---|
| date | date | Local date. |
| page_visits | int | Steam traffic. |
| wishlists_total | int | Current total. |
| wishlists_delta | int | Daily delta. |
| visit_to_wishlist_rate | float | wishlists_delta / page_visits. |
| top_source | text | UTM/source if available. |
| announcement_posted | bool | Did Steam event/news post happen? |
| demo_available | bool | Demo state. |
| notes | text | Context. |

Targets:

- source: `INTERNAL_ASSUMPTION` until Steam page telemetry exists;
- pre-demo page: 5-12% visit-to-wishlist;
- after weak page iteration: under 5% means fix page;
- Next Fest: >15% demo-download-to-wishlist if demo is strong.

### Short-Form Clip

| Field | Type | Notes |
|---|---|---|
| clip_id | text | Match capture naming. |
| platform | enum | TikTok, YouTube Shorts, Reels, X, Bluesky. |
| hook_type | enum | pressure, sonar, salvage, machine, Seed Ship. |
| views | int | Platform. |
| three_second_hold | float | Target >65%. |
| completion_rate | float | Target >40% for 20s. |
| click_rate | float | Wishlist clicks / views. |
| comments_clone_ratio | float | Clone comments / total meaningful comments. |
| comments_unique_feature_ratio | float | Comments naming pressure/machine/salvage/etc. |

Kill criteria:

- completion under 25% across 10 clips;
- clone ratio dominates;
- no one names a HECTON-8-specific feature.

### Creator Outreach

| Field | Type | Notes |
|---|---|---|
| lead_id | text | Stable ID. |
| segment | enum | survival, horror, Subnautica, sim, indie, regional. |
| priority | enum | A/B/C/RAW. |
| status | enum | Use live CRM values only: `VERIFY_BEFORE_CONTACT`, `NEEDS_ASSET`, `LOW_PRIORITY_VERIFY_LATER`, `DO_NOT_CONTACT`, `CONTACTED`, `REPLIED`, `COVERED`, or `DECLINED`. |
| contact_date | date | If contacted. |
| asset_ids_sent | text | Required before a row is counted as asset-backed outreach. |
| creator_utility_score | int | Required for creator-facing asset sends; 3/4+ for Wave A use. |
| creator_send_gate | enum | Asset-side send gate from metadata; blocked values are not reportable outreach. |
| send_route_class | enum | Official route class used for the send; blank or `unknown` blocks reporting. |
| reply_consent_provenance | enum | Required before a reply is reused outside the original route. |
| send_gate_source | text | CRM/send/access gate or tracker field that allowed the send. |
| reply | enum | none, positive, negative, needs_build, covered. |
| coverage_url | url | If covered. |
| pitch_angle | enum | one primary angle. |
| notes | text | Manual. |

Targets:

- source: `INTERNAL_ASSUMPTION` until UTM/outreach telemetry exists;
- targeted creator reply rate: >5%;
- broad batch under 2% means poor fit/copy;
- no mass outreach until assets exist.
- do not count creator outreach in reply-rate or coverage-rate metrics unless `asset_ids_sent`, `creator_utility_score`, `creator_send_gate`, `send_route_class`, `reply_consent_provenance`, and `send_gate_source` are populated where applicable.

### Community Feedback

| Field | Type | Notes |
|---|---|---|
| post_id | text | Stable. |
| platform | text | Reddit/Steam/etc. |
| route_class | enum | `public_cta`, `no_link_feedback`, `support_route`, `private_playtest`, `creator_reply`, `press_reply`, or `unknown`. |
| consent_provenance | enum | `public_comment`, `invited_feedback`, `support_report`, `playtest_consent`, `creator_reply`, `press_reply`, or `unknown`. |
| asset | text | Screenshot/clip ID. |
| question | text | One feedback question. |
| useful_comments | int | Non-meme feedback. |
| positive_signal | int | Useful positive comments. |
| negative_signal | int | Useful negative comments. |
| clone_comments | int | "Subnautica clone" signal. |
| confusion_comments | int | Did not understand asset. |
| action_taken | text | Recapture/rewrite/ignore. |

Targets:

- useful comments > low-effort reactions;
- confusion below 20% of useful comments;
- clone comments declining over asset iterations.

`unknown` route or consent values are import quarantine values only. They cannot be counted in weekly reports, agency-proof reads, creator/press reply rates, public CTA performance, support trends, or owned-audience metrics until the source row is corrected to a specific route and provenance.

### Imageboard Feedback

Imageboard rows are separate from normal community feedback until independently confirmed. Default evidence class is anecdotal.

| Field | Type | Notes |
|---|---|---|
| imageboard_row_id | text | Stable row id. |
| date_checked | date | Local date. |
| surface | enum | 4chan, Dvach, other. |
| board | text | `/vg/`, `/g/`, `/v/`, `/gd/`, `/ai/`, etc. |
| thread_id_or_url | text | Public thread/catalog URL. |
| thread_status | enum | live, archived, 404, unknown. |
| route_class | enum | Always `no_link_feedback` for HECTON posts; `monitor_only` for passive scans. |
| post_permission_gate | enum | Required for HECTON-originated posts; passive scans use `NOT_HECTON_POSTED_MONITOR_ONLY`. |
| asset_id | text | Exact asset shown, or `none_monitoring`. |
| critique_question | text | Exact question asked; blank only for passive monitoring. |
| useful_signal_count | int | Asset-specific comments only. |
| rejected_noise_count | int | Insults, politics, slurs, engine-war with no asset signal. |
| clone_risk_cue | text | Specific cue named, not "clone vibes." |
| ai_slop_cue | text | Specific cue named. |
| engine_trust_cue | text | Specific cue named. |
| decision_read_named | bool | True only if users named a player action/decision. |
| access_or_key_bait | bool | True if anyone requested keys/build/private access/DM route. |
| confidence | enum | anecdotal, directional, recurring, reject. |
| action | enum | monitor_only, revise_asset, revise_prompt, kill_route, security_hold, no_action. |
| linked_dashboard_row | text | Optional AB/campaign row if independently confirmed. |

Reporting rules:

- `confidence=anecdotal` cannot move Campaign 01 to `KEEP`.
- `directional` requires repeated asset-specific cues in the thread or a second independent imageboard thread.
- `recurring` requires confirmation from non-imageboard source: cold-read, Reddit, Steam, creator, press, or playtest.
- `access_or_key_bait=true` blocks any private-access reporting and requires risk review.
- `route_class=monitor_only` cannot be counted as public engagement.
- Imageboard comments cannot become creator/press/playtester/newsletter/support contacts.

## 2026-05-19 Proof-Gate Dashboard V0

Use this before Steam telemetry exists. It measures whether the project is allowed to move from G0 prep to G1 screenshot drop, then from G1 to Steam page launch.

### Asset Gate Table

| Field | Type | Notes |
|---|---|---|
| asset_id | text | Must match `PLAN-SHOT-*`, `PLAN-CLIP-*`, or `PLAN-CAPSULE-*`. |
| build_id | text | Required after capture. |
| status | enum | `PLANNED_CAPTURE`, `RAW`, `REVISION`, `QA_FAIL`, `APPROVED_INTERNAL`, `APPROVED_PUBLIC`, `DEPRECATED`, `LEGAL_HOLD`. |
| qa_score | int | 0-12 for screenshots, use clip checklist for clips. |
| pain_bucket_answered | text | Private proof bucket from asset metadata; not public comparison copy. |
| pain_proof_score | int | 0-5, from the private pain-proof gate after source/date freshness check. |
| pain_freshness_source | text | Monitoring refresh/source row used for the private pain proof score. |
| pain_freshness_checked_at | date | Date when the pain source was checked for the asset score. |
| public_comparison_gate | enum | `PRIVATE_ONLY_NO_COMPETITOR_COPY`, `INTERNAL_ONLY_NO_PUBLIC_PERFORMANCE_COMPARISON`, or stricter. |
| agency_decision_proof_gate | enum | Metadata value; first-packet advance needs one `AGENCY_PROOF_CANDIDATE`. |
| agency_decision_notes | text | One sentence naming the readable player choice or why the asset is not agency proof. |
| capture_handoff_packet_id | text | Stable first-capture packet ID from the shotlist/metadata handoff. |
| capture_verdict | enum | `KEEP_TESTING`, `REVISE_SCENE`, `HOLD_ASSET`, `KILL_ANGLE`, `AGENCY_MISSING_HOLD`, or `PENDING_CAPTURE`. |
| viewer_named_decision | text | The actual decision a cold viewer named without prompt; required for agency candidates. |
| capture_next_actions | text | Up to three concrete follow-up actions from the handoff packet. |
| cold_read_genre_correct | int | Count of viewers who identify underwater survival. |
| cold_read_player_verb | int | Count of viewers who name player action/problem. |
| cold_read_agency_decision | int | Count of valid blind readers who name the next pressure decision without prompt. |
| clone_comments | int | Count of clone/derivative comments. |
| unreadable_comments | int | Count of darkness/clarity failures. |
| ai_or_concept_comments | int | Count of fake/AI/concept-art suspicion. |
| decision | enum | `KEEP`, `REVISE`, `KILL`, `HOLD`. |
| next_action | text | Capture again, recut, approve, or block. |

Minimum to advance Campaign 01:

- at least 6 real assets are no longer `PLANNED_CAPTURE`;
- at least 4 screenshots score 10/12 or higher;
- each priority asset has `pain_proof_score` 4/5+ with `pain_freshness_source` and `pain_freshness_checked_at` filled, and no public comparison gate violation;
- at least one first-pack asset has `agency_decision_proof_gate = AGENCY_PROOF_CANDIDATE` and 60%+ valid blind readers can name the decision without prompt;
- identity hero or salvage shot passes cold-read genre at 70%;
- no asset selected for lead use has unresolved multiplayer-scope, performance, or AI-looking risk;
- final decision is `KEEP`, not `REVISE`.

### Capture Intake Join

The dashboard does not replace asset metadata or the first-capture handoff packet. For first captures, copy only the following facts from `Data/MARKETING_ASSET_METADATA_TEMPLATE.csv` and `Content/SCREENSHOT_AND_CLIP_SHOTLIST.md` after the metadata row and handoff packet are updated:

| Dashboard field | Metadata source | Gate |
|---|---|---|
| `asset_id` | `asset_id` | Must already exist or be justified by a new non-duplicate hook. |
| `file_path` | `path` / first-capture handoff packet | Cannot be blank, guessed, or point to a planned placeholder. |
| `build_id` | `build_id` | Cannot be `TBD`, `latest`, or guessed. |
| `status` | `status` | Use `RAW`, `REVISION`, `QA_FAIL`, `APPROVED_INTERNAL`, or `APPROVED_PUBLIC`; do not dashboard stale `PLANNED_CAPTURE` rows as proof. |
| `qa_score` | `qa_score` | Must come from `QA/MARKETING_ASSET_QA_CHECKLIST.md`. |
| `rejection_code` | `rejection_code` / first-capture handoff packet | Required for failed attempts; blank failure rows are not reportable proof. |
| `creator_rows_unlocked` | `creator_rows_unlocked` | Required before asset-backed creator outreach is counted. |
| `creator_utility_score` | `creator_utility_score` | Required for creator-facing use; 3/4+ for Wave A. |
| `creator_send_gate` | `creator_send_gate` | Blocked values prevent creator reporting even if QA score is high. |
| `pain_bucket_answered` | `pain_bucket_answered` | Private priority only; never public copy. |
| `pain_proof_score` | `pain_proof_score` | 0 until QA assigns it; first-pack priority requires 4/5 after source/date freshness check. |
| `pain_freshness_source` | `pain_freshness_source` | Must name the monitoring refresh/source row used for nonzero pain proof. |
| `pain_freshness_checked_at` | `pain_freshness_checked_at` | Must be same-day for any SN2-derived pain bucket; current-week is not enough for first-pack priority. |
| `public_comparison_gate` | `public_comparison_gate` | Must stay `PRIVATE_ONLY_NO_COMPETITOR_COPY` or stricter for first-pack use. |
| `agency_decision_proof_gate` | `agency_decision_proof_gate` | Must be present; first-pack advance needs one `AGENCY_PROOF_CANDIDATE`. |
| `agency_decision_notes` | `agency_decision_notes` | Must name the readable choice or explain non-proof status; blank notes force `decision=HOLD`. |
| `capture_handoff_packet_id` | `capture_handoff_packet_id` | Must point to the first-capture packet; planned rows stay `PENDING_CAPTURE_PACKET`. |
| `capture_verdict` | `capture_verdict` | `AGENCY_MISSING_HOLD`, `REVISE_SCENE`, `HOLD_ASSET`, or `KILL_ANGLE` blocks Campaign 01 and agency-proof reporting. |
| `viewer_named_decision` | `viewer_named_decision` / AB-009 row | Required before an agency candidate can drive Campaign 01, Steam, creator, press, or weekly agency-proof reporting. |
| `capture_next_actions` | `capture_next_actions` | Required for every failed or held packet; action list must be capped at three. |

If a dashboard row contains a value not present in metadata, QA, or the first-capture handoff packet, mark `decision=HOLD` and fix the source row first. If the handoff packet records `AGENCY_MISSING_HOLD`, Campaign 01 and agency-proof reporting remain held.

### Cold-Read Response Table

Use with `Experiments/A_B_TESTING_AND_CREATIVE_EXPERIMENTS.md` Cold-Read Score Sheet V0.

| Field | Type | Notes |
|---|---|---|
| response_id | text | Stable anonymous id. |
| experiment_id | text | `AB-001`, `AB-002`, `AB-004`, `AB-006`, `AB-007`, or `AB-009`. |
| asset_id | text | Exact `PLAN-*` asset or copy variant. |
| reader_type | enum | internal, player, creator, press, unknown. |
| context_exposure | enum | `NONE`, `CONTEXT_EXPOSED`, `PROMPT_ECHO`, `UNKNOWN`. |
| valid_blind_read | bool | True only when `context_exposure=NONE`. |
| genre_correct | bool | True only if the reader names survival/underwater/exploration/base-adjacent genre without prompt. |
| player_verb_correct | bool | True only if the reader names action/problem, not just mood. |
| what_decision_next | text | Raw answer to the agency prompt; empty answers count as no. |
| agency_decision_read | bool | True only if the reader names repair, retreat, reroute, scan, operate, abort, recover, or an equivalent pressure decision without prompt. |
| identity_nouns | text | Pressure, machinery, salvage, base, black water, Seed Ship, etc. |
| mode_assumption | enum | single-player, unsupported_multiplayer_assumption, multiplayer_question, unknown. |
| proof_belief | enum | gameplay, concept, AI-looking, unsure. |
| readability_issue | enum | none, too_dark, too_busy, ui_unclear, unknown. |
| click_interest | int | 0-4. |
| kill_reason | text | Verbatim where possible. |
| decision_impact | enum | keep, revise, kill, ignore_noise. |

Do not merge cold-read response rows into public engagement metrics. Cold-read answers decide whether an asset is allowed to face public traffic at all. Contaminated reads can create fix notes but cannot count toward pass percentages.

### First Public Beat Table

| Field | Type | Notes |
|---|---|---|
| beat_id | text | Example: `screenshot_drop_01_x_post_001`. |
| asset_id | text | Exact asset used. |
| platform | text | X, Bluesky, Reddit, Steam, YouTube. |
| campaign | text | `screenshot_drop_01`, `steam_page_launch`, etc. |
| route_class | enum | `no_link_feedback`, `public_cta`, `support_route`, `private_access`, or `unknown`. |
| cta_packet_id | text | Required when `route_class=public_cta`; blank for no-link feedback. |
| access_route_class | enum | Required when the beat came from key, private preview, Steam Playtest, tester recruitment, or demo outreach. |
| reply_consent_provenance | enum | Required before private-access or tester feedback is reused outside its original route; use the same field name as creator, press, and curator trackers. |
| agency_decision_field_source | text | Required when access/demo/playtest copy claims gameplay/pressure/route-risk proof. |
| post_url | url | Blank until public. |
| useful_comments | int | Non-meme feedback. |
| intended_nouns | int | Comments naming pressure, machine, salvage, base, black water, Seed Ship. |
| agency_decision_read_comments | int | Comments naming a concrete player choice, not just danger or mood. |
| confusion_comments | int | Comments asking what the game/action is. |
| clone_comments | int | Direct derivative comparison. |
| multiplayer_scope_comments | int | Assumes, asks for, or reads unsupported multiplayer scope into the asset/copy. |
| imageboard_signal_row | text | Optional row id from Imageboard Feedback; cannot be sole `ADVANCE` evidence. |
| imageboard_action | enum | monitor_only, revise_asset, revise_prompt, kill_route, security_hold, no_action. |
| decision | enum | `ADVANCE`, `HOLD`, `REVISE`, `KILL`. |

Decision rule:

- `ADVANCE` only if intended nouns outnumber confusion + clone + multiplayer-scope comments.
- `REVISE` if interest exists but confusion repeats.
- `KILL` if lead asset causes clone, AI-looking, or false-feature damage.
- raw likes do not affect the decision.
- imageboard signal can force `REVISE`, `KILL`, or `HOLD`, but cannot produce `ADVANCE` by itself.

`unknown` route class blocks `ADVANCE`. If the beat used a public link, `cta_packet_id` must point to a destination whose `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`; if the beat came from private access, `access_route_class`, `reply_consent_provenance`, and any required `agency_decision_field_source` must be non-empty before the row is reportable.

### Weekly Current-State Summary

```text
Week:
Current gate: G0/G1/G2/G3/G4
Assets captured:
Assets approved public:
Campaign 01 decision:
Steam page status:
Creator Wave A status:
Agency proof status:
Spend status:
Route/consent gaps:
Rows excluded for route/permission/provenance gaps:
Top blocker:
Next action:
```

### Survey And Surface Scout Table

Use this for polls, itch/DTF/Habr/wiki scouting, and platform-fit tests. These rows do not create public-route permission.

| Field | Type | Notes |
|---|---|---|
| `scout_row_id` | text | Stable ID, e.g. `SCOUT-ITCH-YYYYMMDD-01`. |
| `date_checked` | date | Same-day source/rule check date. |
| `surface` | enum | `itch_io`, `dtf`, `habr`, `fandom`, `wiki_gg`, `pcgamingwiki`, `igdb`, `reddit`, `game_jolt`, `indiedb`, `moddb`, `steam_community`, `steam_news`, `steam_curator_connect`, `steam_playtest`, `steam_demo`, `steam_next_fest`, `steam_themed_sale`, `steam_broadcast`, `steam_utm_widget`, `steam_visibility`, `steam_deck`, `youtube`, `tiktok`, `twitch`, `kick`, `discord_official`, `external_discord`, `x`, `bluesky`, `instagram`, `threads`, `linkedin`, `mastodon`, `medium`, `hashnode`, `dev_to`, `substack`, `tigsource`, `gamedev_net`, `gamedev_ru`, `stopgame`, `pikabu`, `vc_ru`, `playground_ru`, `kanobu`, `vk_play`, `wegame`, `taptap`, `epic_games_store`, `gog`, `kickstarter`, `backerkit`, `idxbox`, `playstation_partners`, `nintendo_developer_portal`, `geforce_now`, `amazon_luna`, `green_man_gaming`, `kowloon_nights`, `epic_megagrants`, `outersloth`, `devolver_pitch`, `secret_mode`, `tinybuild_pitch`, `fellow_traveller`, `no_more_robots`, `hooded_horse`, `team17_pitch`, `games_press`, `game_press`, `pressengine`, `keymailer`, `lurkit`, `terminals_io`, `woovit`, `key_lynx`, `igf`, `digital_dragons`, `indigo_showcase`, `mdev_showcase`, `xp_game_summit`, `gamediscoverco`, `indiegames_press`, `indie_game_plus`, `future_games_show`, `pc_gaming_show`, `the_mix`, `day_of_the_devs`, `six_one_indie`, `indie_live_expo`, `wholesome_direct`, `develop_brighton`, `pitchyagame`, `indie_cup`, `indiecade`, `devgamm`, `gamescom`, `pax_rising`, `pax_aus_indie_showcase`, `bitsummit`, `tokyo_game_show`, `taipei_game_show`, `gamescom_indie_arena`, `home_of_indies`, `calgary_indie_game_bash`, `chinajoy_game_connection`, `nordic_game`, `reboot_develop`, `debug_indie_game_awards`, `bostonfig`, `indiegamebusiness_pitch_live`, `gdc_pitch`, `game_gauntlet`, `amaze_berlin`, `gamescom_latam_big`, `adventurex`, `ludonarracon`, `cerebral_puzzle_showcase`, `women_led_games`, `dreadxp_pitch`, `indie_horror_showcase`, `magfest_mivs`, `dreamhack_indie_playground`, `dreadxp`, `horror_game_awards`, `pc_gamer`, `gamesradar`, `pcgamesn`, `gamespot`, `pocket_gamer`, `mobygames`, `rawg`, `giant_bomb`, `steamgriddb`, `alpha_beta_gamer`, `gamingonlinux`, `product_hunt`, `neogaf`, `resetera`, `something_awful`, `other`. |
| `official_source_url` | URL | Platform/help/rule URL checked before action. |
| `route_class` | enum | `platform_page_feedback`, `no_link_feedback`, `subreddit_no_link_critique`, `technical_article_feedback`, `technical_article_hold`, `wiki_readiness_monitor`, `wiki_application_hold`, `technical_database_update`, `database_listing`, `artwork_database_hold`, `game_profile_page_hold`, `database_profile_page_hold`, `steam_owner_community_hold`, `steam_owner_announcement_hold`, `curator_connect_hold`, `steam_playtest_hold`, `next_fest_demo_hold`, `steam_event_registration_hold`, `steam_broadcast_hold`, `steam_measurement_hold`, `handheld_compatibility_hold`, `cloud_distribution_hold`, `cloud_platform_monitor`, `platform_holder_program_hold`, `owned_media_clip_test`, `creator_video_coverage_hold`, `creator_stream_coverage_hold`, `discord_open_hold`, `external_discord_critique_hold`, `social_micro_pitch_hold`, `social_short_video_hold`, `professional_business_post_hold`, `federated_social_hold`, `owned_newsletter_hold`, `devlog_forum_thread_hold`, `project_showcase_hold`, `ru_dev_forum_critique_hold`, `ru_media_blog_hold`, `ru_broad_community_hold`, `ru_business_article_hold`, `ru_paid_media_hold`, `ru_editorial_tip_hold`, `regional_storefront_hold`, `secondary_storefront_hold`, `reseller_distribution_hold`, `crowdfunding_hold`, `funding_pitch_hold`, `funding_pitch_monitor`, `grant_application_hold`, `publisher_fit_pitch_hold`, `publisher_fit_kill_by_default`, `pitch_resource_monitor`, `press_distribution_hold`, `presskit_distribution_hold`, `pr_tooling_hold`, `key_distribution_hold`, `creator_key_distribution_hold`, `market_data_monitor`, `industry_showcase_hold`, `major_award_submission_hold`, `hashtag_event_hold`, `showcase_media_nomination_hold`, `indie_showcase_submission_hold`, `showcase_submission_hold`, `physical_showcase_hold`, `regional_eligibility_kill_by_default`, `regional_indie_showcase_hold`, `regional_award_showcase_hold`, `regional_showcase_monitor`, `b2b_event_hold`, `physical_showcase_monitor`, `award_submission_hold`, `b2b_pitch_event_hold`, `accelerator_festival_hold`, `art_experimental_award_hold`, `digital_narrative_festival_hold`, `steam_genre_festival_kill_by_default`, `identity_eligibility_kill_by_default`, `publisher_pitch_hold`, `genre_showcase_hold`, `physical_indie_showcase_hold`, `physical_indie_showcase_monitor`, `horror_showcase_monitor`, `awards_monitor_only`, `demo_press_submission_hold`, `linux_press_hold`, `mainstream_press_tip_hold`, `niche_pc_press_tip_hold`, `platform_mismatch_hold`, `tone_mismatch_kill_by_default`, `non_game_launch_kill_by_default`, `high_risk_forum_monitor`, `forum_monitor_or_paid_ad_hold`, `monitor_only`. |
| `permission_gate_source` | text | Exact internal gate or HOLD reason. |
| `asset_or_artifact_id` | text | Exact screenshot/clip/build/page/mock/code/profiler artifact. |
| `survey_packet` | enum | `SURVEY_ASSET_5SEC`, `SURVEY_CLIP_15SEC`, `SURVEY_PAGE_READ`, `SURVEY_SURFACE_FIT`, `SURVEY_DEMO_EXIT`, `none`. |
| `consent_provenance` | text | Feedback-only, no-link critique, public comment, owned form, or explicit opt-in source. |
| `shill_read` | int | Count of blind readers who call it ad/spam/shill. |
| `useful_answer_count` | int | Count of answers naming a concrete asset/content/technical value. |
| `confusion_count` | int | Count of answers with wrong genre/mode/platform/action read. |
| `route_risk` | enum | `LOW`, `MEDIUM`, `HIGH`, `KILL`. |
| `decision` | enum | `HOLD`, `PREP`, `READY_FOR_HUMAN_REVIEW`, `KILL`. |

Decision rules:

- Habr row is `KILL` if the artifact cannot stand as technical content without HECTON-8 branding.
- Wiki rows stay `HOLD` until public/demo/EA content density and moderation owner exist.
- itch.io row stays `HOLD` until a real playable artifact/page draft exists and metadata/platform claims are exact.
- DTF row stays `HOLD` if the post needs a store link to have value.
- Reddit row is `KILL` if the post cannot stand without an external link or the same asset was already posted to another subreddit that day.
- Game Jolt, IndieDB, and ModDB rows stay `HOLD` until the page can be maintained with factual media/build/status and owner-controlled account custody.
- Steam community/news rows stay `HOLD` until Steam page, support/moderation, and announcement gates allow the exact action.
- Owned short-video rows stay `HOLD` unless the first 3 seconds show a readable action, not just mood.
- Devlog/forum rows stay `HOLD` if they are one-off advertisements instead of a thread, project page, or technical critique with a reply owner.
- RU broad/community rows stay `HOLD` unless the post has Russian-language owner coverage, no-link value, and no commercial/ad read.
- PR/key-distribution rows stay `HOLD` until presskit, build/access, official inbox, private-access, disclosure, and tracking gates exist.
- Showcase rows stay `HOLD` until current official deadlines, fees, required footage, and playable-build requirements are checked.
- Database rows stay `HOLD` unless every field is sourced from public official facts, not marketing intent.
- Steam-native rows stay `HOLD` until the exact Steam page/build/access/support/event gates exist; Playtest signup, demo availability, Curator Connect, and Next Fest are separate routes.
- Secondary storefront rows stay `HOLD` until account custody, build/package truth, compliance/rating work, support route, and Steam-truth parity exist.
- Creator stream/video rows stay `HOLD` until build/access, known issues, disclosure wording, support owner, and manual recipient fit exist.
- Discord rows stay `HOLD` until moderation/support owners, rules, invite/access gates, and consent separation exist.
- Social micro-pitch rows stay `HOLD` until account custody, real media, post gate, no-spam cadence, and CTA gate if linked exist.
- Technical publishing/newsletter rows stay `HOLD` until the artifact can stand without selling the game and owned-audience consent is clean.
- Kickstarter/BackerKit rows stay `HOLD` unless campaign budget, rewards, delivery, legal/account custody, and production proof exist.
- High-risk forum rows stay `monitor_only` unless there is already a thread requiring one factual disclosed answer.
- Steam themed event rows stay `HOLD` until the Steam page tags, store description, event eligibility, invite/registration source, and demo/upcoming/released state align.
- Steam broadcast rows stay `HOLD` until broadcast permission, demo/build stability, moderation/support owner, and live/rebroadcast labeling exist.
- Steam UTM/widget/visibility rows are measurement only and cannot report raw visits, stream views, or wishlists as demand proof.
- Media-showcase rows stay `HOLD` until current trailer/gameplay, presskit, nomination route, deadline/source, and post-show owner exist.
- Wholesome/tone-mismatch rows are `KILL` by default unless a human exception proves HECTON pressure/noir identity stays intact.
- Mainstream press-tip rows stay `HOLD` until there is a real news beat, official inbox custody, presskit, footage/demo, and unsupported claims removed.
- Platform-specific press rows stay `HOLD` unless the platform state is measured or otherwise publicly proven.
- Physical/regional showcase rows stay `HOLD` until playable booth/demo proof, travel/booth/hardware/staffing owners, event deadline/source, and post-show follow-up owner exist.
- Regional eligibility rows are `KILL` if location, language, legal, or local-partner requirements are not proven from current official rules.
- B2B pitch/event rows stay `HOLD` until business deck, current demo/build, explicit ask, legal/business owner, target list, and CRM follow-up route exist.
- Award submission rows stay `HOLD` until category fit, fee/deadline, eligibility, build/media proof, public-facts source, and disclosure requirements are checked; public selection claims are forbidden before acceptance.
- Stale event-rule rows stay `monitor_only` until a current official application/submission source replaces old PDFs, archive pages, or third-party news.
- Genre/direct rows are `KILL` if fit is theme-only, lore-only, opportunistic, or dishonest.
- Digital narrative and Steam genre festival rows stay `HOLD` until narrative/puzzle mechanic proof, Steam/demo/build state, source/deadline, support owner, and CTA separation exist.
- Horror publisher and horror showcase rows stay `HOLD` until build/deck/trailer, timeline, budget, team profile, deal boundaries, horror/noir fit proof, and disclosure requirements exist.
- Art/experimental award rows stay `HOLD` until experimental interaction proof, fee/deadline, art statement, video, and onsite/online owner exist.
- Identity eligibility rows are `KILL` if team/leadership/audience eligibility and owner approval are absent.
- Physical local indie station rows stay `HOLD` until station stability, original-asset/content policy, hardware, staffing/travel, queue/crash plan, and acceptance-language proof exist.
- Creator key hub rows stay `HOLD` until private-access gate, build/access truth, key batch cap, manual recipient approval, official inbox custody, disclosure, access log, support owner, and grey-market monitoring exist.
- Presskit/tooling/newswire rows stay `HOLD` until a real news beat or demo-access truth, presskit URL, official inbox, release format, embargo state, claim lint, and spend decision are source-backed.
- Major award/festival rows stay `HOLD` until playable/media/category/eligibility/fee/deadline proof exists; selection, finalist, nomination, exhibition, and award language are forbidden before acceptance proof.
- B2B and industry-showcase rows stay `HOLD` until business ask, pitch deck, playable demo, target list, legal/business owner, and CRM follow-up route exist.
- Market-data/newsletter rows are `monitor_only` unless a separate source-backed contact/submission route exists; data mentions cannot become public demand proof.
- Platform-holder rows stay `HOLD` until company/legal owner, account custody, port budget, controller UX, certification/compliance owner, rating/content owner, support route, and platform approval boundaries exist.
- Handheld compatibility rows stay `HOLD` until Steam app/build, device or Proton test, controller-only path, text-input path, UI/readability proof, performance capture, and public compatibility claim boundary exist.
- Cloud distribution rows stay `HOLD` or `monitor_only` until store ownership, opt-in/application state, login/account-linking answer, cloud-save state, controller UX, support owner, and availability-claim proof exist.
- Funding, grant, and publisher-fit rows stay `HOLD` until playable proof, deck/document, budget/timeline, business ask, legal/business owner, deal boundaries, and partner-fit reason exist.
- Publisher-fit kill-by-default rows stay `KILL` unless the real build and partner catalogue fit are source-backed; logo exposure is not a valid reason.
- Regional storefront rows stay `HOLD` until build/package, account custody, legal/payment/compliance owner, localization/support owner, and Steam/public truth parity exist.
- Platform-mismatch rows stay `KILL` or `HOLD` unless the platform scope is real; mobile-only routes cannot be used for a PC-only game without mobile build and support proof.
- RU media/community rows stay `HOLD` until RU owner coverage, current rules/source, disclosure/ad separation, one real artifact, and no-link/news/technical value exist.
- Reseller/distribution rows stay `HOLD` until commercial owner, pricing/discount policy, key policy, support/refund owner, territory/currency answer, and store-truth parity exist.
- Database/artwork catalog rows stay `HOLD` unless every field or image is sourced, neutral, rights-clean, duplicate-checked, and consistent with the public Steam/site truth.
- Pitch resource rows are `monitor_only` or `HOLD` until they become a real partner route with playable build, deck, ask, legal owner, fit statement, and CRM follow-up.
- Any row with blank `official_source_url`, `route_class`, `consent_provenance`, or `asset_or_artifact_id` is excluded from weekly signal.

## Weekly Report Format

Title:

`Marketing Weekly YYYY-MM-DD`

Sections:

1. Steam movement.
2. Best asset.
3. Worst asset.
4. Creator pipeline movement.
5. Community feedback themes.
6. Agency-decision read rate.
7. Competitor signal.
8. Decisions.
9. Kill criteria triggered.
10. Next week work.

## Dashboard Rules

- Do not average across different asset types without segmenting.
- Do not compare paid and organic traffic directly.
- Do not count bot/spam comments as signal.
- Do not treat wishlist total as proof of game quality.
- Do not celebrate impressions if no wishlist or creator action follows.
- Do not hide negative "clone" signal; it is the main differentiation warning.
- Do not merge feedback, form, or support rows without `route_class` and `consent_provenance`; creator/press/curator rows use `send_route_class` and `reply_consent_provenance`; private access rows use `access_route_class` and `reply_consent_provenance`.
- Do not count support reports, playtest forms, creator replies, or press replies as newsletter, CRM, or public marketing consent.
- Do not count a gameplay/pressure/route-risk asset as agency proof unless `agency_decision_read`, `agency_decision_read_comments`, or `cold_read_agency_decision` records the decision readers named.
- Do not count key/access/playtest/demo outreach replies or claims unless `access_route_class`, `reply_consent_provenance`, and `agency_decision_field_source` are present where relevant.
- Do not count creator, press, curator, support, public CTA, private access, or owned-audience rows whose permission gate/source is blank or whose route/provenance field is `unknown`.
- Do not count imageboard signal as positive proof unless independently confirmed; use it primarily to revise, kill, or monitor.
- Do not count key-hub, PR-tooling, newswire, B2B, award, or market-data rows as outreach, coverage, selection, or demand proof unless the matching gate/source fields are filled and the row is not `HOLD` or `monitor_only`.
- Do not count platform-holder, handheld, cloud, publisher, fund, or grant rows as availability, funding, certification, publisher interest, or platform support unless approval/contract/proof artifacts exist.
