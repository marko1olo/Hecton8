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
| Lead Verifier | recent activity, fit, contact route, language | approve outreach batch |
| Pitch Writer | tailored pitch variants | approve exact copy |
| Steam Copy Tester | short/long description variants | cold-reader test |
| Sentiment Scout | SN2/survival pain points | evidence classification |
| Clip Hook Writer | 15-20s hook variants | match to real footage |
| Reddit Drafter | critique post drafts | subreddit rule check |
| Compliance Checker | no co-op, no fake claims, disclosure rules | mandatory |
| Metrics Analyst | wishlist/traffic/demo reporting | daily review |
| Key Fraud Triage | suspicious key requests | deny/approve list |

## Daily Pre-Asset Loop

1. Mine 20 creator leads.
2. Verify 10 of them.
3. Assign segment and pitch angle.
4. Add one evidence note.
5. Reject any lead with no clear fit.
6. Produce 5 post hooks.
7. Produce 3 Steam copy variants.
8. Check all copy against forbidden language.

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

- `RAW_SEED`
- `VERIFY_BEFORE_CONTACT`
- `READY_FOR_HUMAN_REVIEW`
- `APPROVED_TO_CONTACT`
- `CONTACTED`
- `REPLIED`
- `DECLINED`
- `COVERED`
- `DO_NOT_CONTACT`

## Outreach Batch Protocol

Never contact hundreds at once.

Batch sizes:

- cold early screenshots: 10-20 high-fit leads;
- first gameplay clip: 20-40;
- demo preview: 50-100;
- Next Fest: 100-200 segmented contacts.

Each batch must have:

- one asset link;
- one pitch angle;
- one CTA;
- one tracking tag;
- no co-op language;
- no performance language without proof.

## Prompt Templates For Future Agents

### Lead Miner Prompt

Find public creator leads for HECTON-8, a single-player-first NASA-punk / deep-sea noir underwater survival game. Segment by Subnautica, survival crafting, horror atmosphere, engineering/simulation, indie showcase, Steam demo/Next Fest, and regional languages. Do not fabricate contact info or subscriber counts. Mark all unverified rows `VERIFY_BEFORE_CONTACT`.

### Pitch Verifier Prompt

Given this creator lead list, verify each public page for recent activity, audience fit, language, and safe contact route. Remove any co-op pitch language. Return only rows that a human can review for contact.

### Copy Compliance Prompt

Check this marketing copy for forbidden claims: co-op, multiplayer, Subnautica killer, fake performance proof, unsupported realism, hostile competitor attack. Rewrite into single-player-first pressure/machinery/salvage positioning.

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
