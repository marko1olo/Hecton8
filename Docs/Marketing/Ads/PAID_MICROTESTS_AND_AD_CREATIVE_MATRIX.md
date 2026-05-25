# HECTON-8 Paid Microtests And Ad Creative Matrix

Status: pre-spend plan
Owner lane: SHINOBU_81 / low-budget paid tests
Runtime impact: none

## Rule

Paid ads are last-mile tests, not a rescue strategy. If the Steam page, capsule, screenshot, or demo is weak organically, paid traffic burns money faster.

## Spend Gate

No paid ads until:

- Official CTA Link Activation Gate V0 passes for the Steam destination;
- UTM tracking works;
- capsule passes cold-read;
- first screenshot/clip passes QA;
- any ad that uses gameplay, pressure, route-risk, threat, salvage failure, or first-public proof has one factual agency candidate with non-pending asset metadata `viewer_named_decision`, valid non-held `capture_verdict`, and AB-009/KPI decision-read fields: `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`;
- conversion baseline exists from organic traffic;
- one hypothesis is written;
- stop rule is written.

## 2026-05-20 Paid Spend Permission Boundary V0

PMT rows are hypotheses, not spend permission.

Use `spend_permission_gate` as the only machine-readable field for paid ad spend:

- current rows must stay `BLOCKED_*`;
- a PMT row can launch only when `spend_permission_gate = ALLOW_PAID_MICROTEST_VERIFIED`;
- `ALLOW_PAID_MICROTEST_VERIFIED` requires Official CTA Link Activation Gate V0 for the Steam destination, Steam URL custody, UTM proof, Campaign 01 `KEEP` or an equivalent organic page baseline, asset QA, non-pending `viewer_named_decision`, valid non-held `capture_verdict`, AB-009/KPI decision-read fields where the ad sells gameplay/pressure/route-risk proof, a capped budget, a written hypothesis, a written stop rule, and an owner able to inspect Steam/UTM results within 48 hours.

Do not infer paid permission from budget tier, PMT ID, platform candidate, or a completed creative row.

## Budget Tiers

| Budget | Use |
|---:|---|
| 50 USD | One micro-test for click/capsule/short clip. |
| 150 USD | 2-3 variants after one organic winner. |
| 500 USD | Only after Steam page has acceptable conversion. |
| 1000+ USD | Only near demo/launch with proven hook. |

## Platforms

| Platform | Use | Risk |
|---|---|---|
| Reddit ads | Niche targeting and critique-adjacent tests. | Can be hostile to ads; weak creative dies. |
| YouTube Shorts/Google | Video hook tests. | Harder attribution and creative fatigue. |
| TikTok | Short clip algorithm tests. | Wrong audience if clip is not instantly readable. |
| X/Twitter | Dev/press/creator reach. | Low conversion, noisy. |
| Meta | Broad visual tests. | Often weak for PC survival unless targeting is sharp. |

## Creative Families

| Family | Asset | Headline | CTA |
|---|---|---|---|
| Pressure | hatch/leak/gauge | Survive below the light | Wishlist on Steam only after `spend_permission_gate = ALLOW_PAID_MICROTEST_VERIFIED`, `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`, and destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` |
| Machinery | pump/base/tool | Keep the machines alive | Wishlist on Steam only after `spend_permission_gate = ALLOW_PAID_MICROTEST_VERIFIED`, `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`, and destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` |
| Seed Ship | anomaly/signal | Something is buried below | Wishlist on Steam only after `spend_permission_gate = ALLOW_PAID_MICROTEST_VERIFIED`, `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`, and destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` |
| Salvage | route/wreck | Salvage under pressure | Play demo or wishlist only after `spend_permission_gate = ALLOW_PAID_MICROTEST_VERIFIED`, `demo_public_access_permission_gate` where a demo is linked, `steam_page_publish_permission_gate` where Steam is linked, and destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` |
| Base risk | flooded module | Your base can fail | Play demo or wishlist only after `spend_permission_gate = ALLOW_PAID_MICROTEST_VERIFIED`, `demo_public_access_permission_gate` where a demo is linked, `steam_page_publish_permission_gate` where Steam is linked, and destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` |

## 2026-05-20 Paid Microtest Execution Plan

Status: blocked until Steam page, UTM, cold-read, and organic baseline exist.

The first paid spend is not a campaign. It is AB-008 from `Experiments/A_B_TESTING_AND_CREATIVE_EXPERIMENTS.md`: a capped smoke test to see whether the winning capsule/copy can move tracked Steam traffic.

| Paid test | Spend permission gate | Required winner before spend | Platform candidate | Max spend | Asset/UTM content | Audience | Pass signal | Stop rule |
|---|---|---|---|---:|---|---|---|---|
| PMT-001 Pressure identity smoke | `BLOCKED_NO_STEAM_BASELINE` | AB-001 screenshot winner + AB-002 capsule winner + AB-009 agency candidate with non-pending metadata handoff if copy implies route risk | Reddit ads or X small test | 50 USD | `PLAN-SHOT-001` or `PLAN-SHOT-003` + `PLAN-CAPSULE-001`; add `PLAN-SHOT-006`, `PLAN-CLIP-001`, or `PLAN-CLIP-003` only after metadata handoff and AB-009/KPI decision-read fields exist | Survival/exploration/underwater-adjacent | At least 50 clicks and no dominant confusion comments. | Stop if "what do you do?", "AI art", pending metadata, or unsupported multiplayer-scope expectation dominates. |
| PMT-002 Clip hook smoke | `BLOCKED_NO_STEAM_BASELINE` | AB-006 trailer opening winner + metadata handoff plus AB-009/KPI decision-read evidence if clip is sold as pressure gameplay | YouTube Shorts/Google or TikTok small test | 75 USD | `PLAN-CLIP-001` or `PLAN-CLIP-003` if agency proof is claimed; `PLAN-CLIP-002` only for identity/mood hook, not agency proof | Short-form PC survival/horror-adjacent | First 3 seconds holds attention and sends measurable Steam visits. | Stop if platform gives views but no Steam behavior or viewers cannot name the pressure decision. |
| PMT-003 Steam copy/capsule smoke | `BLOCKED_NO_STEAM_BASELINE` | AB-004 copy winner + AB-002 capsule winner | Reddit/X/Meta tiny test | 100 USD | `PLAN-CAPSULE-001` + short description winner | PC survival players | Steam cold traffic reaches 2-5% wishlist conversion directionally. | Stop within 48h if cold traffic is under 1% or comments show premise confusion. |
| PMT-004 Creator-retarget support | `BLOCKED_NO_CREATOR_SIGNAL_BASELINE` | AB-005 creator signal positive | Platform matching creator audience | 150 USD | Best creator/clip asset | Only after organic creator reply/coverage exists | Paid traffic reinforces a proven hook. | Stop if creator traffic does not produce Steam actions. |

### Paid UTM Defaults

Use only where UTM is allowed.

```text
utm_source=[reddit_ads|x_ads|youtube_ads|tiktok_ads|meta_ads]
utm_medium=ad
utm_campaign=paid_microtest_01
utm_content=[pmt_id]_[asset_id]_[hook]
utm_term=[survival|exploration|base_building|deep_sea|creator_retarget]
```

Examples:

```text
utm_source=reddit_ads&utm_medium=ad&utm_campaign=paid_microtest_01&utm_content=pmt001_plan-shot-001_pressure_identity&utm_term=survival
utm_source=youtube_ads&utm_medium=ad&utm_campaign=paid_microtest_01&utm_content=pmt002_plan-clip-001_pressure_leak&utm_term=deep_sea
```

### Spend Abort Conditions

Abort all paid tests if:

- Steam page warm traffic is below 3% visit-to-wishlist before cold paid traffic starts;
- no capsule/copy winner exists;
- comments show unsupported multiplayer-scope expectation;
- screenshots read as AI/concept art;
- the page needs obvious copy or asset changes;
- the owner cannot inspect Steam/UTM results within 48 hours.

## Bad Ad Copy

Reject:

- "Subnautica killer";
- "finally co-op underwater survival";
- "massive open world";
- "zero stutter";
- "AAA graphics";
- "best survival game";
- "wishlist now!!!"

## Test Template

```text
Test ID:
Spend permission gate:
Budget:
Platform:
Audience:
Asset:
Hook:
CTA:
UTM:
Hypothesis:
Start:
End:
Impressions:
Clicks:
Steam visits:
Wishlists:
Cost per Steam visit:
Cost per wishlist if measurable:
Decision:
```

## Stop Rules

Stop if:

- CTR is bad and comments indicate confusion;
- Steam visits do not wishlist after enough sample;
- ad or landing row claims gameplay/pressure/route-risk proof without non-pending metadata `viewer_named_decision`, valid non-held `capture_verdict`, and `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`;
- people ask if it is multiplayer/co-op due to ad copy;
- ad comments compare asset to AI/concept art;
- cost per useful action exceeds realistic low-budget tolerance;
- platform gives traffic with no Steam behavior.

## Current HECTON-8 Decision

No paid ads now. Current PMT rows remain blocked. Prepare creative matrix and UTMs only; first spend should be 50-150 USD only after Steam page and organic baseline exist and the selected PMT row is explicitly `ALLOW_PAID_MICROTEST_VERIFIED`.
