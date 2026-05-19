# HECTON-8 Launch Day And First Week War Room

Status: launch operations template / no launch scheduled
Owner lane: SHINOBU_81 / launch ops
Runtime impact: none

## Purpose

The first week decides whether public attention becomes wishlists/sales/feedback or collapses into confusion and support debt. This document defines the command structure before Early Access, demo, or full launch.

## War Room Roles

| Role | Job |
|---|---|
| Launch Lead | Final go/no-go, message control. |
| Steam Ops | Page, build, discount, events, announcements. |
| Support Lead | Bugs, forums, known issues, crash triage. |
| Community Lead | Discord/Reddit/social replies. |
| Creator Lead | Creator/press replies and access issues. |
| Metrics Lead | UTM, wishlists, sales/demo downloads, source tracking. |
| Build Liaison | Confirms known build state from engineering. |

One person can hold multiple roles if workload is small, but each role must have an owner.

## 7 Days Before

- confirm Steam page copy;
- confirm price/discount if selling;
- confirm demo/build version;
- freeze screenshots/trailer unless critical issue;
- prepare Steam announcement;
- prepare social posts;
- prepare creator/press reminder;
- prepare known issues;
- prepare support templates;
- check no co-op language;
- check no unproved performance claims.

## 48 Hours Before

- final build smoke test;
- final store assets check;
- final UTM check;
- final presskit check;
- verify contact email;
- verify Discord/forum pins;
- verify bug report route;
- verify moderation escalation;
- prepare "launch delayed" fallback post.

## 2026-05-19 War Room Dry Run Gate V0

Run this before any public Steam page launch, demo drop, Early Access launch, Next Fest participation, or large creator batch.

### Minimum Owner Map

| Lane | Minimum named owner | Cannot be blank when |
|---|---|---|
| Go/no-go | Launch Lead | Any public date, demo, page, or access batch exists. |
| Build truth | Build Liaison | Any playable build or performance statement exists. |
| Store truth | Steam Ops | Any Steam page, demo button, announcement, or event exists. |
| Public replies | Community Lead | Any X/Bluesky/Reddit/Discord/Steam/forum surface is active. |
| Support intake | Support Lead | Any demo/playtest/EA route exists. |
| Creator/press | Creator Lead | Any key, presskit, preview, or creator email is sent. |
| Measurement | Metrics Lead | Any public link or UTM is used. |

One human can own several lanes, but no lane can be ownerless. If an owner is not available for the first 24 hours, the campaign is `HOLD`.

### 45-Minute Dry Run Agenda

| Minute | Check |
|---:|---|
| 0-5 | Confirm campaign ID, build ID, asset IDs, official links, and rollback owner. |
| 5-10 | Read the exact public copy through the Promise Lint Gate. |
| 10-15 | Open Steam/site/social/presskit links from a clean browser session. |
| 15-20 | Verify support, bug report, crash/performance, and key-scam routes. |
| 20-25 | Confirm UTM or measurement packet names match the dashboard. |
| 25-30 | Read the top 10 expected negative comments and approved replies. |
| 30-35 | Confirm launch delayed fallback post and no-post abort switch. |
| 35-45 | Record one state only: `GO`, `HOLD`, or `KILL`. |

### Dry Run Kill Conditions

- public copy includes co-op, "zero stutter", exact date, or competitor-attack language;
- Steam/site/demo link cannot be verified from a clean session;
- build ID, asset IDs, and measurement IDs do not match;
- no named owner can answer the first support/performance reports;
- key/access route is not logged;
- first public asset has no QA score or reject-code history.

## Launch Day Timeline

| Time | Action |
|---|---|
| T-2h | Owner check-in, source links, build/page status. |
| T-1h | Steam page/build/announcement final check. |
| T | Launch/page/demo/event live. |
| T+15m | Check Steam page, trailer, demo button, links. |
| T+30m | Social posts. |
| T+1h | Creator/press batch if ready. |
| T+2h | First issue scan. |
| T+4h | Metrics snapshot. |
| T+8h | Known issues update if needed. |
| T+24h | First digest and next-day plan. |

## Launch Metrics

Track:

- Steam visits;
- wishlist delta;
- demo downloads if applicable;
- sales/revenue if selling;
- refund signals when available;
- review count/sentiment;
- top UTM source;
- creator/press mentions;
- Discord joins;
- support issues;
- crash/performance clusters.

## First Week Signal Gates

| Gate | Check | Action |
|---|---|---|
| T+4h | Store/demo links work, no wrong build, no false copy. | If failed, pause amplification and fix links/copy first. |
| T+24h | Top five comments/issues are categorized. | Update Known Issues, FAQ, or page copy; do not argue. |
| T+48h | Wishlist/demo/download trend has a source breakdown. | Expand only sources with useful actions, not raw impressions. |
| T+72h | Repeated confusion tags are below threshold. | If clone/co-op/darkness/objective confusion repeats, revise assets before new outreach. |
| Day 7 | Support burden, reviews, and conversion are readable. | Choose one: `EXPAND`, `REVISE`, `PAUSE`, or `KILL CAMPAIGN`. |

### First Week Expansion Rule

Do not move from warm traffic to paid traffic, press expansion, large creator batch, or regional push until:

- top negative expectation mismatch is known;
- current public copy has been linted after real comments;
- known issues are updated;
- measurement packet has source data;
- no critical route crash/save/load issue is active;
- creator/press replies can be handled within 24 hours.

## Red Alerts

Immediate response required:

- build not downloadable;
- demo launches wrong build;
- crash in first route;
- save/load corruption;
- Steam page has wrong price/discount;
- trailer/asset broken;
- false co-op/performance claim visible;
- key/access leak;
- review/forum wave about misleading copy.

## Holding Statements

### Build Issue

```text
We are investigating a launch build issue affecting [scope]. We will update this thread when we have a verified fix or workaround. Current known issue: [brief].
```

### Performance Cluster

```text
We are collecting hardware/settings reports for the performance issue in [area/build]. Please use the template here: [link]. We will not guess publicly without build and hardware context.
```

### Misleading Expectation

```text
We need to clarify this: HECTON-8 is single-player-first and does not currently include co-op. We are updating copy wherever that was unclear.
```

## First Week Daily Digest

```text
Day:
Top metrics:
Top positive signal:
Top negative signal:
Worst bug/performance cluster:
Top expectation mismatch:
Creator/press status:
Support actions:
Store/copy changes:
Product escalation:
Next 24h:
```

## First Week Priorities

1. Fix blockers.
2. Correct misleading copy.
3. Reply to support threads with facts.
4. Preserve creator/press trust.
5. Do not chase every suggestion.
6. Do not panic-discount.
7. Do not make roadmap promises under pressure.

## Current HECTON-8 Decision

No launch scheduled. This file exists so launch operations are ready before the first public build.
