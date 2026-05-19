# HECTON-8 Paid Microtests And Ad Creative Matrix

Status: pre-spend plan
Owner lane: SHINOBU_81 / low-budget paid tests
Runtime impact: none

## Rule

Paid ads are last-mile tests, not a rescue strategy. If the Steam page, capsule, screenshot, or demo is weak organically, paid traffic burns money faster.

## Spend Gate

No paid ads until:

- Steam page exists;
- UTM tracking works;
- capsule passes cold-read;
- first screenshot/clip passes QA;
- conversion baseline exists from organic traffic;
- one hypothesis is written;
- stop rule is written.

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
| Pressure | hatch/leak/gauge | Survive below the light | Wishlist on Steam |
| Machinery | pump/base/tool | Keep the machines alive | Wishlist on Steam |
| Seed Ship | anomaly/signal | Something is buried below | Wishlist on Steam |
| Salvage | route/wreck | Salvage under pressure | Play demo / Wishlist |
| Base risk | flooded module | Your base can fail | Play demo / Wishlist |

## 2026-05-19 Paid Microtest Execution Plan

Status: blocked until Steam page, UTM, cold-read, and organic baseline exist.

The first paid spend is not a campaign. It is AB-008 from `Experiments/A_B_TESTING_AND_CREATIVE_EXPERIMENTS.md`: a capped smoke test to see whether the winning capsule/copy can move tracked Steam traffic.

| Paid test | Required winner before spend | Platform candidate | Max spend | Asset/UTM content | Audience | Pass signal | Stop rule |
|---|---|---|---:|---|---|---|---|
| PMT-001 Pressure identity smoke | AB-001 screenshot winner + AB-002 capsule winner | Reddit ads or X small test | 50 USD | `PLAN-SHOT-001` or `PLAN-SHOT-003` + `PLAN-CAPSULE-001` | Survival/exploration/underwater-adjacent | At least 50 clicks and no dominant confusion comments. | Stop if "what do you do?", "AI art", or co-op confusion dominates. |
| PMT-002 Clip hook smoke | AB-006 trailer opening winner | YouTube Shorts/Google or TikTok small test | 75 USD | `PLAN-CLIP-001` or `PLAN-CLIP-002` | Short-form PC survival/horror-adjacent | First 3 seconds holds attention and sends measurable Steam visits. | Stop if platform gives views but no Steam behavior. |
| PMT-003 Steam copy/capsule smoke | AB-004 copy winner + AB-002 capsule winner | Reddit/X/Meta tiny test | 100 USD | `PLAN-CAPSULE-001` + short description winner | PC survival players | Steam cold traffic reaches 2-5% wishlist conversion directionally. | Stop within 48h if cold traffic is under 1% or comments show premise confusion. |
| PMT-004 Creator-retarget support | AB-005 creator signal positive | Platform matching creator audience | 150 USD | Best creator/clip asset | Only after organic creator reply/coverage exists | Paid traffic reinforces a proven hook. | Stop if creator traffic does not produce Steam actions. |

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
- comments show co-op expectation;
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
- people ask if it is co-op due to ad copy;
- ad comments compare asset to AI/concept art;
- cost per useful action exceeds realistic low-budget tolerance;
- platform gives traffic with no Steam behavior.

## Current HECTON-8 Decision

No paid ads now. Prepare creative matrix and UTMs. First spend should be 50-150 USD only after Steam page and organic baseline exist.
