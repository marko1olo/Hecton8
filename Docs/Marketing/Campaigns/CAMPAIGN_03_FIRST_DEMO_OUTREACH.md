# Campaign 03 - First Demo Outreach

Status: future / requires stable demo
Public stance: single-player-first scope / proof-first campaign copy
Runtime impact: none

## Objective

Use the first demo, after all demo gates pass, to convert send-verified creators and Steam visitors with a playable proof slice. This is the first point where review keys and broader creator outreach make sense.

## Demo Gate

Do not launch this campaign until:

- public demo/Steam Playtest access has `demo_public_access_permission_gate = ALLOW_PUBLIC_DEMO_ACCESS_VERIFIED`, or any private demo/key/preview access has `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`;
- demo has a start, goal, tension beat, and ending;
- first 20 minutes are understandable without a developer call;
- no critical save/load or crash issue in the demo route;
- controls are explainable in-game;
- the first route includes one readable pressure decision that feedback can code as `AGENCY_DECISION_READ`;
- any demo outreach claim about gameplay, pressure, route risk, threat, salvage, base failure, or first-public agency proof has `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`;
- performance claims are either omitted or backed by hardware/settings proof;
- any key/private-preview route has recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`, official inbox custody, exact access-log fields, and disclosure.

## Demo Slice Design For Marketing

The demo should contain:

1. short descent;
2. first tool/machine interaction;
3. salvage route;
4. pressure or oxygen cost;
5. base/habitat safety moment;
6. one warning or threat that forces a readable repair, retreat, reroute, scan, or recover decision;
7. Seed Ship or anomaly tease;
8. clean endpoint with approved Steam/demo CTA after `Analytics/MEASUREMENT_AND_UTM_PLAN.md` Official CTA Link Activation Gate V0 passes; otherwise no-link feedback endpoint.

## Creator Batch Plan

Batch A: up to 30 human-verified high-fit send candidates after official contact route, build, asset, key, creator utility, AB-009/KPI agency-decision proof where claimed, `creator_send_gate`, `send_route_class` for creator sends, `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED` plus exact private access-log fields where access is used, `reply_consent_provenance`, and CRM send-log gates pass.

- 10 direct underwater survival;
- 8 survival route risk;
- 5 engineering/base systems;
- 5 horror/abyss pressure;
- 2 regional high-fit.

Batch B only if Batch A works:

- 50 more triaged candidates after official-route verification and asset fit scoring;
- no paid slots unless demo retention is strong and the selected CRM row has `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED`.

## 2026-05-19 Demo Access Batch Scoring V0

Before sending a demo/key to any creator or press contact, score the row. Use the current CRM/contact file and do not infer contact permission from a third-party index.

Route separation rule: public Steam/demo/Playtest access must pass `demo_public_access_permission_gate = ALLOW_PUBLIC_DEMO_ACCESS_VERIFIED`, and public Steam/demo CTA links must pass `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`. Private demo/key/preview routes must pass `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED` in `Press/REVIEW_KEYS_EMBARGO_AND_PREVIEW_ACCESS_PROTOCOL.md` and log `verified_contact_route`, `access_route_class`, `reply_status_after_send`, `reply_consent_provenance`, and `agency_decision_field_source` where proof claims are used; private routes stay out of public posts, bios, showcase CTAs, and presskit download links.

| Field | Score |
|---|---:|
| Official contact route verified this week | 0-2 |
| Recent relevant upload/stream activity | 0-2 |
| Audience fit for single-player pressure/machinery/survival | 0-3 |
| Required proof asset matches the creator format | 0-3 |
| Demo beat includes readable pressure decision | 0-2 |
| AB-009/KPI decision-read field exists for the pressure/route-risk claim | 0-2 |
| Send/access route fields and `reply_consent_provenance` ready | 0-2 |
| Demo route fits the creator's normal video length | 0-2 |
| Brand-safety and scam risk cleared | 0-2 |
| Coverage value for a small project | 0-2 |
| Risk penalty: stale, broad variety, multiplayer-mode-focused, drama-prone, or paid-only | -3 to 0 |

### Send Thresholds

| Total | State | Action |
|---:|---|---|
| 15+ | `SEND_CANDIDATE` | Human may send after asset/build/key gates, creator utility, AB-009/KPI agency proof where claimed, `creator_send_gate`, official route, `send_route_class` for creator sends or exact access-log field set for private access, `reply_consent_provenance`, and CRM send-log readiness pass. |
| 10-14 | `HOLD_FOR_BETTER_ASSET` | Wait for a more specific clip, demo beat, send/access field readiness, or Steam proof. |
| 6-9 | `LOW_PRIORITY_VERIFY_LATER` | Do not include in the first two waves. |
| 0-5 | `DO_NOT_CONTACT_NOW` | Leave out unless the project changes substantially. |

### Batch A Composition Rule

Batch A is max 30 sends, but the first live cut is smaller:

| Slice | Count | Required proof |
|---|---:|---|
| A1 direct underwater/survival | 8-10 | Demo route + Steam page + 2 matching screenshots + AB-009/KPI agency field if the pitch claims pressure/route-risk proof. |
| A2 engineering/base systems | 4-6 | Machine/base clip proving interaction, not decoration, with `send_route_class` or exact private access-log fields plus `reply_consent_provenance` ready. |
| A3 horror/abyss pressure | 4-6 | Sound/visibility/threat clip with clear objective, one readable retreat/scan/reroute decision, and AB-009/KPI field source. |
| A4 regional/native language | 2-4 | Reviewed localized pitch and region-safe page copy. |
| A5 press-friendly creators | 2-4 | Presskit, no-embargo access terms, and press `send_route_class` for outreach or exact private access-log fields for access routes, plus `reply_consent_provenance`. |

### Stop Rules During Batch A

Stop sending more demo outreach for 48 hours if any of these occur:

- two creators cannot explain the objective or name the pressure decision after 10 minutes;
- two creators ask if multiplayer is planned because the copy implies it;
- two creators say the footage is too dark to read;
- one credible creator reports a first-route crash/save/load blocker;
- one key/access leak or impersonation issue appears;
- Steam page conversion or demo completion is unreadable because links/UTMs were wrong.

## Key Policy

Use Steam keys only after:

- identity verified;
- official contact route confirmed;
- key logged;
- matching CRM send-log fields ready for the real send;
- AB-009/KPI field source recorded for gameplay/pressure/route-risk claims;
- `send_route_class` for sends or exact private access-log fields for access routes, plus `reply_consent_provenance`, ready;
- disclosure language included;
- scam impersonation check passed.

No loose keys in Discord DMs.

## Demo Pitch

Subject:

HECTON-8 demo - single-player pressure/machinery underwater survival

Message:

Hi [Name],

Your channel fits because [specific verified pattern].

The HECTON-8 demo is a playable slice of single-player underwater survival focused on pressure, machinery, salvage, and black-water route risk. The angle stays proof-backed and competitor-neutral.

Agency proof source: [AB-009/KPI field ID or omit this sentence if not available]

Suggested coverage angle for your audience:

[segment-specific angle]

Demo/key: [private access route or key batch ID after access log]
Steam: [gated public Steam URL after `steam_page_publish_permission_gate` and `public_cta_permission_gate` pass]
Press kit: [gated presskit URL after `press_release_permission_gate` and `public_cta_permission_gate` pass]

Useful disclosure line if you cover it: code/key provided by the developer.

Thanks,
[Name]

## Segment-Specific Coverage Angles

| Segment | Coverage angle |
|---|---|
| Direct underwater survival | "How different does the pressure/machinery tone feel from bright underwater exploration?" |
| Survival route risk | "Can you complete a salvage route and return without overextending?" |
| Engineering/base systems | "Does the base feel like a working pressure vessel or decoration?" |
| Abyss horror pressure | "Does dread come from instruments, sound, and pressure rather than jumpscares?" |

## Metrics

| Metric | Target |
|---|---:|
| Creator positive replies from Batch A | 5+ |
| Published/streamed coverage from Batch A | 3+ |
| Demo median playtime | 15+ minutes |
| Demo completion rate | 25%+ |
| Players can name one pressure decision | 60%+ in feedback rows that are not prompt-echoed |
| Agency decision field stored | 100% before pressure/route-risk proof is reused in outreach copy |
| Route/provenance recorded | 100% before replies count outside original route |
| Steam wishlist lift during campaign | measurable daily increase |
| Critical crash reports | 0 blocking route issues |
| Repeated "too dark" feedback | under 25% |

## Kill Criteria

Stop sending keys if:

- the demo crashes in the first route;
- creators cannot understand the objective;
- players cannot name a decision under pressure;
- more than 30% complain about controls/readability;
- players say "this is just a mood demo";
- Steam conversion does not move after coverage.
