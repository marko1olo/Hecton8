# HECTON-8 90-Day Marketing Operations Calendar

Status: reusable schedule template
Owner lane: Marketing / operations
Runtime impact: none

## Authority Boundary

Static schedule template only. Public voice routes through root `textes.md`.
Any quality, release, platform, Steam, wishlist, demo, performance, press, review-key, embargo, showcase, curator, asset-QA, schedule, SEO, pricing, discount, publication, website, or presskit claim requires `quality.md`, `release.md`, `platform.md`, current proof artifacts, and the matching local permission gate.
Calendar rows, planned weeks, checklist items, and strategy rows do not approve Steam page, wishlist, pricing, discount, review-key, presskit, showcase, publication, platform, or release claims.

## Assumption

This calendar starts when the team can produce at least rough in-game screenshots within 30 days. If screenshots are farther away, repeat Weeks 1-4 as preparation loops.

Public scope stays single-player-first. Paid scaling waits for proof. Performance language waits for measured evidence.

## 2026-05-19 Scheduling Override

Current gate is `G0`: no public screenshot pack, no Steam page, no demo, no public traffic, no AB-009/KPI agency-decision proof, and no filled `send_route_class` / `reply_consent_provenance` fields. CRM-100 already has 0 raw staged rows.

Until rough in-game screenshots exist, do not execute this calendar as a lead-volume calendar. Use the calendar only as a sequence model and keep weekly work focused on:

- planned capture readiness;
- asset QA;
- Promise Lint;
- source/risk corrections tied to a gate;
- owner-controlled account/handle custody only if the human has project credentials ready.

Weeks 3 and 8 are not authorization to verify/send more creators now. They activate only after the asset gate can identify which creator segment is worth contacting, and gameplay/pressure/route-risk sends have `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision` plus `send_route_class` / `reply_consent_provenance` custody.

## Phase Overview

| Phase | Days | Goal | Public output |
|---|---:|---|---|
| P0 | 1-14 | Infrastructure and asset criteria | none or light dev presence |
| P1 | 15-30 | First screenshot pack readiness | critique-ready screenshots |
| P2 | 31-45 | Steam page lock | Steam Coming Soon candidate |
| P3 | 46-60 | Creator/press warmup | verified outreach batch |
| P4 | 61-75 | First gameplay clip wave | short clips and devlog |
| P5 | 76-90 | Demo/Next Fest prep | demo-facing pitch and dashboards |

## Week 1 - Lock Public Identity

Tasks:

- confirm multiplayer-scope boundary in all copy;
- choose 3 public pillars: pressure, machinery, Seed Ship anomaly;
- audit existing screenshots/concepts for overly clean sci-fi read;
- create denylist of forbidden claims;
- assign one marketing owner for source ledger.

Deliverables:

- updated `BRAND_AND_POSITIONING_BIBLE.md`;
- current `Data/SOURCE_LEDGER.md`;
- 20 candidate one-liners;
- 10 thumbnail text candidates.

Kill criteria:

- if no one can explain the game in one sentence, do not produce assets yet.

## Week 2 - Build Asset QA Factory

Tasks:

- prepare screenshot scorecard;
- prepare clip scorecard;
- prepare Steam capsule brief;
- prepare UTM naming;
- create asset archive naming convention.

Deliverables:

- QA checklist completed;
- `AB-001` and `AB-004` experiment briefs;
- internal review form.

## Week 3 - Lead Verification Sprint 1

Tasks:

- verify 50 creator leads from the priority shortlist only if the current CRM/asset-fit gate has a real gap;
- mark active/inactive/language/contact route;
- promote only high-fit leads to warm list;
- write 20 personalized opener drafts;
- create denylist for scams/off-fit channels.
- do not promote any gameplay/pressure/route-risk opener to send-later unless it names the needed AB-009/KPI field and planned route class.

Deliverables:

- 50 verification-gated rows; official contact permission is not inferred from third-party indexes;
- 20 send-later messages blocked behind asset QA, AB-009/KPI agency-decision proof where relevant, `creator_send_gate`, `send_route_class`, and `reply_consent_provenance`;
- no outreach yet unless asset exists.

## Week 4 - Screenshot Pack 0

Tasks:

- capture 20 rough in-game screenshots;
- score each with QA checklist;
- kill anything below 9/12;
- choose 6 candidate Steam screenshots;
- run cold-reader test, including AB-009/KPI agency-decision fields for any asset that claims pressure, route risk, threat, salvage failure, or base failure.

Deliverables:

- Screenshot Pack 0;
- cold-reader results with `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision` where agency proof is claimed;
- revised shotlist.

Decision:

- If no screenshot scores 10/12, delay public screenshot drop.

## Week 5 - Steam Page Draft

Tasks:

- draft short description variants;
- draft long description;
- select preliminary tags;
- prepare Steam capsule placeholders;
- create language priority list.

Deliverables:

- Steam copy package;
- tag V0;
- localization queue.

## Week 6 - First Screenshot Drop Rehearsal

Tasks:

- run screenshot A/B internally;
- prepare 3 Reddit critique versions;
- prepare 3 X/Bluesky versions;
- prepare 3 creator micro-pitch versions;
- prepare UTM links only after Official CTA Link Activation Gate V0 passes for the Steam destination;
- hold every gameplay/pressure/route-risk public or creator variant until AB-009/KPI agency-decision proof exists.

Deliverables:

- campaign doc ready;
- asset QA signoffs.

## Week 7 - Steam Page Publish Gate

Tasks:

- final check of Steam asset requirements;
- final check of tags;
- final check of multiplayer-scope copy;
- final check of AB-009/KPI agency-decision fields for first-page agency proof;
- setup UTM naming;
- prepare first Steam announcement draft.

Deliverables:

- Steam page candidate;
- presskit shell;
- website one-pager draft.

Decision:

- Publish only if capsule, screenshots, short description, tags, official CTA/UTM, and first-page agency proof pass cold-reader and AB-009/KPI decision-read gates.

## Week 8 - Outreach Warm Batch

Tasks:

- send 20 high-fit creator warm emails/DMs only if assets exist, `creator_send_gate` is open, AB-009/KPI decision-read fields exist for gameplay/pressure/route-risk claims, and `send_route_class` plus `reply_consent_provenance` are ready;
- send no keys from build existence alone; require official inbox custody, recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`, exact access-log fields, disclosure, and the relevant creator/press/curator send gate;
- log every message;
- run first press soft pitch only if presskit exists and press `send_route_class` / `reply_consent_provenance` fields are ready;
- monitor reply quality.

Deliverables:

- 20 outreach rows;
- reply taxonomy;
- next batch edits.

## Week 9 - First Gameplay Clip Pack

Tasks:

- capture 10 clips;
- choose 3 hook families;
- edit 6 short versions;
- run AB-003 and AB-006;
- run AB-009/KPI decision-read pass on any clip that sells pressure, route risk, threat, salvage failure, or base failure;
- update pitch bank with winning clip.

Deliverables:

- Clip Pack 1;
- creator preview link;
- revised trailer beat sheet.

## Week 10 - Community Proof Week

Tasks:

- post one useful devlog;
- post one critique request in rule-compatible community;
- do not cross-post same copy;
- answer comments with specifics;
- log pain points, route-specific class, `consent_provenance` or `reply_consent_provenance`, and whether replies named a player decision before counting them as KPI signal.

Deliverables:

- feedback digest;
- community rules tracker updates;
- FAQ additions.

## Week 11 - Paid Micro-Test Readiness

Tasks:

- choose one winning screenshot/capsule/clip;
- confirm Steam UTM works;
- define 25-150 USD test;
- write stop rule;
- do not run if Steam page is weak or if gameplay/pressure/route-risk creative lacks AB-009/KPI agency-decision proof.

Deliverables:

- paid micro-test brief;
- budget approval note.

## Week 12 - Regional Preparation

Tasks:

- localize Steam short copy to RU/DE/ES/PT-BR if justified;
- verify 25 regional creators only if asset/route gaps exist after current CRM-100 and regional copy has `send_route_class` / `reply_consent_provenance` handling;
- prepare regional pitch variants;
- check cultural/genre fit.

Deliverables:

- regional outreach pack;
- localized one-pager drafts.

## Week 13 - Demo / Next Fest Readiness Review

Tasks:

- audit demo-facing claims;
- verify Steam Next Fest eligibility from official docs;
- prepare demo creator pitch;
- prepare bug-report intake;
- prepare launch-day dashboard.

Deliverables:

- demo outreach checklist;
- Next Fest rule recheck note;
- go/no-go report.

## Weekly Dashboard

Track every Monday:

| Metric | Target before scaling |
|---|---:|
| Verification-gated creator rows | +25/week minimum |
| Warm creator replies | 10-20% from high-fit batch |
| Screenshot score | 10/12 for Steam assets |
| Correct genre read | 70%+ cold readers |
| Agency decision read | AB-009/KPI field exists before agency-proof reporting |
| Route/provenance custody | `send_route_class`, `access_route_class`, `route_class`, and `reply_consent_provenance` fields filled as route-appropriate before replies count as KPI signal |
| Steam UTM tracked visits | growing, source-separated |
| Wishlist conversion from trusted visits | source-dependent; compare channels |
| Useful comments | 10+ per meaningful public beat |
| Negative confusion themes | decreasing week over week |

## Daily Micro-Loop

Every day after screenshots exist:

1. Verify 5-10 leads only if the current asset-fit gate shows a real gap.
2. Improve one pitch only inside the current asset/AB-009/route gate.
3. Improve one asset/copy line.
4. Check one platform/source rule.
5. Log one insight.

Before screenshots exist, use `Operations/DAILY_AGENT_TASK_LOOP.md` instead: planned capture readiness, asset QA, Promise Lint, source/risk corrections, and custody gates.

If nothing was logged, the day did not produce marketing infrastructure.
