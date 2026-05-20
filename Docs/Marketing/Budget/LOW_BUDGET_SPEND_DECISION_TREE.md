# Low Budget Spend Decision Tree

Status: budget guardrail / pre-screenshot
Assumption: "several thousand USD" total marketing cash
Public stance: single-player-first scope / proof-first public copy
Runtime impact: none

## Core Rule

Do not buy reach before the page and assets convert.

Money cannot fix a screenshot that reads generic, a capsule that reads muddy, a demo that confuses players, or a pitch that looks like spam.

## Spend Priority

| Priority | Spend | Why |
|---:|---|---|
| 1 | Steam capsule/key art polish | The capsule is the ad for every organic impression. |
| 2 | Trailer/clip edit and audio polish | Short clips carry creator, Steam, Reddit, TikTok, and press beats. |
| 3 | Localization review for top regions | Bad translated copy kills trust immediately. |
| 4 | Small paid creator tests | Only after organic fit is proven and the selected CRM row has `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED`. |
| 5 | Paid ads | Last, small, and stopped fast if conversion is weak. |

## Budget Scenario A - 1000 USD

| Spend | Max | Gate |
|---|---:|---|
| Capsule/key art polish | 400 | First screenshot pack has proven identity. |
| Trailer/clip edit | 300 | Real footage exists. |
| Localization review RU/DE/PT-BR snippets | 150 | Steam page or demo is close. |
| Tooling/misc | 100 | Tracking, hosting, press kit polish. |
| Paid ads | 50 | Only smoke test after Steam page conversion baseline. |

Do not buy sponsored videos at this level unless a creator is extremely high fit, cheap, and the selected CRM row has `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED`.

## Budget Scenario B - 3000 USD

| Spend | Max | Gate |
|---|---:|---|
| Capsule/key art polish | 700 | 3 roughs tested internally. |
| Trailer/edit/audio | 700 | Demo or real gameplay route exists. |
| Localization review | 300 | RU/DE/PT-BR/ES short copy. |
| Paid creator tests | 800 | Organic creator response is positive and the selected CRM row has `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED`. |
| Paid ad tests | 200 | Steam page converts warm traffic. |
| Contingency | 300 | Fix the thing that metrics prove is broken. |

Best use: one strong capsule, one strong trailer/clip pack, 2-4 small creator experiments.

## Budget Scenario C - 5000 USD

| Spend | Max | Gate |
|---|---:|---|
| Capsule/key art + variants | 900 | Cold-read tested. |
| Trailer/edit/audio + short cuts | 1200 | Real footage and demo route exist. |
| Localization review/subtitles | 600 | Regions show demand or high-fit creators. |
| Paid creator tests | 1500 | Demo stable, verified creator fit, and selected CRM row has `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED`. |
| Paid ad tests | 400 | Steam conversion acceptable. |
| Press kit/website polish | 200 | `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`, public CTA gate for any link, and presskit/public-release gate for any public presskit claim. |
| Contingency | 200 | Only metric-driven fixes. |

Best use: Steam page polish, demo campaign support, 5-8 small creator tests, localized clips.

## Spending Gates

### Capsule Spend Gate

Spend if:

- first screenshot pack has a clear identity;
- cold viewers understand the genre;
- HECTON-8 logo/mark exists or can be final enough;
- capsule artist gets a strict brief.

Do not spend if:

- visual direction is still generic;
- no one can define the main silhouette;
- the art brief says "like Subnautica but darker".

### Trailer Spend Gate

Spend if:

- footage has player verbs;
- at least one pressure/machinery consequence is visible;
- pressure, route-risk, threat, or salvage-failure footage has AB-009/KPI decision-read evidence if the trailer will sell agency proof;
- there is a Seed Ship/anomaly tease;
- audio can be improved from real game direction.

Do not spend if:

- footage is only swimming/scenery;
- UI is placeholder and embarrassing;
- there is no playable loop;
- trailer would need fake cinematics to look alive.

### Creator Spend Gate

Spend if:

- creator is verified;
- audience fit is high;
- demo is stable;
- CRM row has `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED`;
- the creator brief and asset packet pass `creator_send_gate`, `send_route_class`, disclosure, official route, and AB-009/KPI decision-read gates for any gameplay/pressure/route-risk claim;
- deliverables are clear;
- disclosure is included;
- the creator is not demanding false talking points.

Do not spend if:

- contact route is unverified;
- the creator is multiplayer-mode-only;
- they ask for keys through a suspicious route;
- they require "Subnautica killer" framing;
- the demo cannot survive public play.

### Paid Ads Gate

Spend only if:

- Steam page has baseline conversion;
- capsule is strong;
- the selected PMT row has `spend_permission_gate = ALLOW_PAID_MICROTEST_VERIFIED`;
- Campaign 01/PMT candidate retains AB-009/KPI decision-read evidence if the ad sells pressure gameplay or route risk;
- the ad links to a page, not a vague social post;
- test budget is capped;
- stop rule is written before launch.

Stop if:

- warm traffic wishlist conversion is below 3%;
- click cost is high and wishlist rate is weak;
- comments show confusion;
- page changes are clearly needed.

## 2026-05-20 Current Spend Release Ladder

Status: all cash spend frozen until asset gates pass.

| Release step | Max spend | Required proof | Allowed spend | Stop condition |
|---:|---:|---|---|---|
| 0 | 0 USD | No public assets yet. | Agent prep, copy, CRM, QA, cold-read setup. | Stop making docs if no row, asset gate, or decision changes. |
| 1 | 0-50 USD | `PLAN-SHOT-001/003` captured and AB-001/AB-002 ready. | Human cold-reader pool or tiny feedback tool cost. | Under 70% genre clarity or capsule unreadable. |
| 2 | 50-150 USD | Campaign 01 returns `KEEP`, Official CTA Link Activation Gate V0 passes for the Steam destination, UTM works, selected PMT row has `spend_permission_gate = ALLOW_PAID_MICROTEST_VERIFIED`, and PMT pressure/route-risk claims have AB-009/KPI decision-read fields. | One PMT row only: PMT-001 or PMT-002. | No useful Steam behavior, missing agency read, or dominant confusion within 48h. |
| 3 | 300-800 USD | Capsule AB-002 winner is clear and Steam page warm traffic is not failing. | Capsule/key-art polish. | No variant beats plain readable logo/silhouette. |
| 4 | 300-1000 USD | `PLAN-CLIP-*` has a strong first 3 seconds and demo/route footage exists. | Trailer edit/audio polish. | Clip needs fake cinematics or cannot show player verb. |
| 5 | 250-1500 USD | Organic creator replies prove fit, demo/preview build is stable, and selected CRM row has `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED`. | 1-3 paid creator tests, disclosed. | Creator requires false framing, disclosure/route proof is missing, or demo cannot survive coverage. |

Do not buy Step 3/4/5 before Step 2 proves the Steam page can receive traffic without obvious confusion.

### Current Recommendation

As of 2026-05-20: spend `0 USD`. Use agent labor to get asset IDs, QA gates, creator packet, Steam assembly, and response matrices ready. The first cash outlay should be cold-reader help or one capped PMT smoke test only after real assets, Official CTA Link Activation Gate V0 for any paid public link, and `spend_permission_gate = ALLOW_PAID_MICROTEST_VERIFIED` on the selected PMT row.

## What Not To Buy

- Fake wishlists.
- Fake Discord members.
- Fake Steam reviews.
- Key-reseller exposure.
- Untargeted influencer databases.
- Generic PR blast lists.
- Broad Facebook/Google ads before Steam page proof.
- Expensive cinematic trailer before gameplay proof.
- "Guaranteed coverage" packages from unknown sites.

## Human Approval Checklist

Before any spend:

- [ ] What exact file/page/asset will this improve?
- [ ] What metric proves we need it?
- [ ] What is the maximum loss?
- [ ] What is the stop rule?
- [ ] Is the selected PMT row explicitly `spend_permission_gate = ALLOW_PAID_MICROTEST_VERIFIED`?
- [ ] If this is paid creator spend, is the selected CRM row explicitly `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED`?
- [ ] If spend uses gameplay/pressure/route-risk proof, where is `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision` recorded?
- [ ] Does it require a feature claim we cannot prove?
- [ ] Does it imply unsupported multiplayer scope?
- [ ] Does it make us look derivative?
- [ ] Is there a cheaper agent/manual version first?
