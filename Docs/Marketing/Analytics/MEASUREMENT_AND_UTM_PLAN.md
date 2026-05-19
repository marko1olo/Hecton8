# Measurement And UTM Plan

Status: instrumentation plan / pre-public
Public stance: single-player-first / no co-op promise
Runtime impact: none

## Objective

Measure which hooks create Steam interest before spending serious money. The project has limited cash, so every public push must answer one question: did this asset or audience move wishlists, demo plays, or useful feedback?

## Measurement Boundary

Do not pretend this is perfect attribution. Steam hides and aggregates data in ways we cannot fully control. Use this plan to compare directionally:

- source;
- campaign;
- asset;
- creator segment;
- region;
- date;
- Steam page conversion;
- demo play data when available.

## Naming Scheme

Use a stable naming format for every external link where UTM is allowed:

`utm_source=[platform]`
`utm_medium=[post_creator_press_ad]`
`utm_campaign=[campaign_phase]`
`utm_content=[asset_or_creator]`
`utm_term=[segment]`

Examples:

- `utm_source=youtube&utm_medium=creator&utm_campaign=demo_batch_a&utm_content=igp&utm_term=direct_underwater_survival`
- `utm_source=reddit&utm_medium=post&utm_campaign=screenshot_drop_01&utm_content=base_pressure_vessel&utm_term=visual_critique`
- `utm_source=press&utm_medium=article&utm_campaign=steam_page_launch&utm_content=rockpapershotgun&utm_term=pc_indie_press`

## 2026-05-19 Experiment And Asset UTM Registry

Status: pre-public / use only after official link exists.

Use the same IDs across experiment docs, ad docs, asset metadata, and reports. Do not invent per-platform names.

### `utm_content` Format

| Use case | Format | Example |
|---|---|---|
| Organic screenshot post | `[asset_id]_[hook]` | `plan-shot-001_pressure_identity` |
| Organic clip post | `[asset_id]_[hook]` | `plan-clip-003_salvage_failure` |
| A/B test | `[experiment_id]_[asset_id]_[variant]` | `ab-001_plan-shot-001_a` |
| Paid microtest | `[pmt_id]_[asset_id]_[hook]` | `pmt-001_plan-shot-001_pressure_identity` |
| Creator outreach | `[creator_slug]_[asset_id_or_demo]` | `wanderbots_plan-clip-003` |
| Press beat | `[outlet_slug]_[beat]` | `pcgamer_steam_page_launch` |

### Canonical Test IDs

| ID | Measurement meaning |
|---|---|
| `ab-001` | First screenshot identity vs verb cold-read test. |
| `ab-002` | Capsule rough readability/preference test. |
| `ab-003` | Base risk vs heavy machinery organic post test. |
| `ab-004` | Short description clarity test. |
| `ab-005` | Creator micro-pitch asset fit test. |
| `ab-006` | Trailer opening hook test. |
| `ab-007` | Steam tag proof test. |
| `ab-008` | Paid micro-test gate. |
| `pmt-001` | Pressure identity paid smoke test. |
| `pmt-002` | Clip hook paid smoke test. |
| `pmt-003` | Steam copy/capsule paid smoke test. |
| `pmt-004` | Creator-retarget paid support test. |

### UTM Kill Rules

- Do not use `subnautica`, `sn2`, `coop`, `zero_stutter`, or `100km` in UTM names.
- Do not use a creator name in `utm_content` unless the creator actually received a link.
- Do not reuse an `ab-*` or `pmt-*` ID for a different asset or audience.
- Do not shorten links through opaque services until raw UTM behavior has been checked.

## Campaign IDs

| Campaign ID | Meaning |
|---|---|
| `pre_screenshot_setup` | Internal/soft prep. No broad public CTA. |
| `screenshot_drop_01` | First screenshot critique and identity test. |
| `steam_page_launch` | Coming Soon page launch. |
| `demo_batch_a` | First verified creator demo outreach batch. |
| `demo_batch_b` | Second demo outreach batch, only if A works. |
| `next_fest_event` | Steam demo event / Next Fest participation. |
| `regional_push_ru` | RU/CIS localized push. |
| `regional_push_de` | German localized push. |
| `regional_push_ptbr` | Brazil/Portuguese push. |

## Data Tables

### Daily Steam Funnel

| Date | Page visits | Wishlists | Visit-to-wishlist % | Demo downloads | Demo launches | Demo median minutes | Notes |
|---|---:|---:|---:|---:|---:|---:|---|

### Campaign Event Log

| Date/time | Campaign | Asset | Link | Audience | Cost | Expected signal | Result |
|---|---|---|---|---|---:|---|---|

### Creator Attribution

| Creator | Segment | Link sent | UTM content | Date sent | Response | Coverage date | Steam traffic window | Wishlist lift | Notes |
|---|---|---|---|---|---|---|---|---:|---|

### Feedback Coding

| Source | Asset | Positive signal | Negative signal | Product issue | Marketing issue | Action |
|---|---|---|---|---|---|---|

## 2026-05-19 Minimum Measurement Packet Before Public Links

Create these IDs before posting any public URL. If there is no official Steam or landing URL, leave the link blank and record only qualitative feedback.

| Packet | Required ID | Required fields | Blocker |
|---|---|---|---|
| Asset packet | `asset_id` | build, status, hook, QA score, owner, rejection code if any. | Do not post if asset is still `PLANNED_CAPTURE` or `RAW`. |
| Campaign packet | `campaign_id` | campaign, platform, asset_id, CTA type, owner. | Do not run if no keep/revise/kill rule exists. |
| Link packet | UTM URL | `utm_source`, `utm_medium`, `utm_campaign`, `utm_content`, `utm_term`. | Do not invent UTM for platforms that disallow or strip it. |
| Feedback packet | `beat_id` | post URL, useful comments, confusion, clone, co-op, decision. | Do not summarize comments without counts. |
| Spend packet | `pmt_id` | max spend, AB winner, target, stop rule, result. | Do not spend before Steam baseline and UTM work. |

### Canonical First-Beat IDs

| Beat | ID | Notes |
|---|---|---|
| First identity post | `screenshot_drop_01_identity_post` | Usually `PLAN-SHOT-001`. |
| First base/machinery critique | `screenshot_drop_01_base_critique` | Usually `PLAN-SHOT-002` or `PLAN-SHOT-005`. |
| First salvage/action post | `screenshot_drop_01_salvage_post` | Usually `PLAN-SHOT-003` or `PLAN-CLIP-003`. |
| First capsule critique | `screenshot_drop_01_capsule_critique` | `PLAN-CAPSULE-001`, no Steam CTA unless page exists. |
| Steam page live | `steam_page_launch_announcement` | Requires official Steam URL and Campaign 01 `KEEP`. |

### First Link Rules

- Use no tracking link in Reddit critique posts unless community rules allow it.
- Prefer raw Steam URL in public replies; use UTM only in planned posts/bios where accepted.
- Do not use shorteners until first raw UTM behavior is known.
- Do not put competitor terms in campaign, content, or term fields.
- If a platform strips UTM, still record the post URL and asset ID in the dashboard.

## Metrics To Trust

Trust more:

- wishlist conversion from warm traffic;
- demo completion and median playtime;
- creator reply rate;
- repeated feedback patterns;
- screenshot cold-read clarity;
- regional traffic after localized pushes.

Trust less:

- raw likes;
- raw impressions;
- Discord member count;
- short-form views without Steam clicks;
- one angry comment;
- one high-view creator with no Steam movement.

## Targets By Phase

| Phase | Useful target | Stop/revise threshold |
|---|---:|---:|
| Screenshot critique | 70% cold viewers identify genre | under 50% |
| Screenshot critique | under 20% "clone" comments | over 30% |
| Steam page warm traffic | 5-10% wishlist conversion | under 3% |
| Steam page cold traffic | 2-5% wishlist conversion | under 1% |
| Creator critique batch | 3 replies from 20 sends | 0 replies |
| Demo creator batch | 5 positive replies from 30 sends | under 2 |
| Demo | 15+ min median playtime | under 8 min |

## Weekly Report Format

Every week:

1. What was posted.
2. What assets were used.
3. What source produced traffic.
4. What converted.
5. What confused players.
6. What to kill.
7. What to double down on.

Template:

```md
## Week YYYY-MM-DD

- Best signal:
- Worst signal:
- Highest converting source:
- Lowest converting source:
- Top repeated praise:
- Top repeated complaint:
- Product issue to send to dev:
- Marketing copy/asset issue:
- Spend recommendation:
- Next test:
```

## Rules

- No paid ad expansion without conversion baseline.
- No creator batch expansion without reply/coverage signal.
- No screenshot direction survives if cold viewers cannot read it.
- No performance copy goes live without proof artifact.
- No co-op-related UTM terms or campaigns.
