# Agent Marketing Workflows

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## R4 Interior Actuality Boundary

This document is active only as static documentation/source/platform-orientation. Current authority is `AGENTS.md`, `.agents-skills`, `Docs/Actual Domains of Project.txt`, current source files, current official platform rules, current verification artifacts, and the latest DOC_GLOBAL reports.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, public Steam page, public demo, wishlist performance, creator outreach readiness, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters, platform rules, dates, and marketing claims inside this file are subordinate to fresh official sources and current project proof.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Status: operational playbook for future agents

## Agent Roles

| Role | Output | Human Gate |
|---|---|---|
| Lead Miner | raw creator/press/social leads | verify before contact |
| Lead Verifier | recent activity, fit, contact route, language | prepare send-review packet; cannot override asset/send gates |
| Pitch Writer | tailored pitch variants | approve exact copy |
| Steam Copy Tester | short/long description variants | cold-reader test |
| Sentiment Scout | SN2/survival pain points | evidence classification |
| Clip Hook Writer | 15-20s hook variants | match to real footage |
| Reddit Drafter | critique post drafts | subreddit rule check |
| Compliance Checker | scope-safe copy, no fake claims, disclosure rules | mandatory |
| Metrics Analyst | wishlist/traffic/demo reporting | daily review |
| Key Fraud Triage | suspicious key requests | deny/approve list |

## Daily Pre-Asset Loop

Current override: CRM already has 100 staged rows and 0 raw rows. Before screenshots/clips exist, agent time goes to proof assets and safe routing, not lead volume.

1. Prepare or score the planned screenshot/clip packet.
2. Fill or audit asset metadata claim gates: `multiplayer_scope_check`, `performance_claim_check`, and `feature_truth_check`.
3. Match existing `NEEDS_ASSET` and `VERIFY_BEFORE_CONTACT` rows to exact planned asset IDs.
4. Check creator-facing candidates against `creator_utility_score`, `creator_send_gate`, `agency_decision_proof_gate`, AB-009/KPI decision-read fields where the copy claims gameplay/pressure/route-risk proof, named CRM row, and send-log fields.
5. Add or revise 5 asset-linked post hooks.
6. Run Promise Lint on any copy that could become public or creator-facing.
7. Recheck contact routes only when a passing asset creates a send need.
8. Update source ledger/risk register only when evidence changes a gate.
9. Open raw lead mining only by explicit source-backed sprint after first assets reveal a real audience gap.

After any changed file, run `Operations/DAILY_AGENT_TASK_LOOP.md` End-Of-Change Validation Cut V0.

## Weekly Competitive Report

Sections:

- SN2 update/news summary.
- Survival crafting trends.
- Underwater/thalassophobia posts.
- Creator videos gaining traction.
- Steam tags used by adjacent games.
- Player pain themes.
- HECTON-8 actionable implications.

Evidence rules:

- link every claim;
- label source class;
- do not infer mechanics from vibes;
- no fake top comments;
- no "players hate X" unless there is recurring signal.

## Lead Database Schema

Use these fields:

- `segment`
- `priority`
- `name`
- `platform`
- `url`
- `language`
- `fit_reason`
- `recent_activity`
- `contact_route`
- `contact_status`
- `risk_notes`
- `pitch_angle`
- `last_verified`
- `source`
- `do_not_contact_reason`

Allowed status values:

- `VERIFY_BEFORE_CONTACT`
- `NEEDS_ASSET`
- `LOW_PRIORITY_VERIFY_LATER`
- `DO_NOT_CONTACT`
- `CONTACTED`
- `REPLIED`
- `DECLINED`
- `COVERED`

Raw mining sheets may use local raw-queue states such as `RAW_PUBLIC_INDEX_NOT_CONTACT_READY`, but those states are not live CRM statuses and must not be copied into `Data/CREATOR_VERIFICATION_TEMPLATE.csv` without a deliberate schema migration.

## Outreach Batch Protocol

Never contact hundreds at once.

Batch sizes:

- cold early screenshots: 10-20 high-fit leads;
- first gameplay clip: 20-40;
- demo preview: 50-100;
- Next Fest: 100-200 segmented contacts.

Each batch must have:

- one real asset ID/link after asset metadata claim checks, QA, creator utility where creator-facing, `creator_send_gate` where creator-facing, and the relevant public/private route gates pass;
- passing asset metadata claim checks;
- creator utility 3/4+ if creator-facing;
- open `creator_send_gate`;
- `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision` if the batch claims gameplay, pressure, route-risk, threat, salvage failure, or first-public agency proof;
- `send_route_class` for creator/press/curator sends, `access_route_class` for private access, and `reply_consent_provenance` before any creator, press, curator, or access send;
- one pitch angle;
- one public CTA only after the exact destination gate passes (`steam_page_publish_permission_gate`, `press_release_permission_gate`, `demo_public_access_permission_gate`, `discord_open_permission_gate`, `owned_audience_permission_gate`, or `steam_support_permission_gate` as applicable) and destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`; otherwise use a no-link feedback ask or a private route only after recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`, official inbox custody, disclosure, and exact access-log fields;
- one tracking tag;
- no multiplayer-scope promise;
- no competitor-attack language;
- no performance language without proof.

## Prompt Templates For Future Agents

### Lead Miner Prompt

Find public creator leads for HECTON-8, a single-player-first NASA-punk / deep-sea noir underwater survival game. Segment by underwater survival, survival crafting, horror atmosphere, engineering/simulation, indie showcase, Steam demo/Next Fest, and regional languages. Do not fabricate contact info or subscriber counts. Keep unverified rows in a raw queue; move only checked rows into live CRM statuses.

### Pitch Verifier Prompt

Given this creator lead list, verify each public page for recent activity, audience fit, language, asset fit, and safe contact route. Remove any multiplayer-scope promise or competitor-attack copy. Return only rows that a human can review for contact.

### Copy Compliance Prompt

Check this marketing copy for forbidden claims: multiplayer-scope promises, competitor-attack positioning, fake performance proof, unsupported realism, or hostile comparison language. Rewrite into single-player-first pressure/machinery/salvage positioning.

### Sentiment Scout Prompt

Monitor current SN2, Subnautica, survival crafting, and underwater horror discussions. Extract recurring player pain themes with source links and evidence labels. Do not use isolated rants as truth. Do not fabricate YouTube comment ranking.

## Human-Only Decisions

Agents do not decide:

- final Steam copy;
- final key art;
- paid ad spend;
- public roadmap;
- feature promises;
- creator payment;
- embargo date;
- launch date;
- Next Fest entry.
