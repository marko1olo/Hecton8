# Creator Contract Terms And Rate Card

Status: negotiation prep / not legal advice
Public stance: single-player-first scope / proof-first creator copy
Runtime impact: none

## Objective

Prepare a sane paid creator framework before anyone asks for money. With a few thousand dollars, paid creator spend must be small, testable, and tied to demo/Steam conversion.

## Paid Slot Types

| Type | Use | Risk |
|---|---|---|
| Dedicated video | Strongest but expensive. | Bad fit burns budget fast. |
| Stream segment | Good for demo play. | Harder attribution, chat can derail. |
| Integrated mention | Lower cost. | Weak if game needs visual proof. |
| Short-form clip | Cheap and reusable. | Views may not convert. |
| Newsletter/press sponsor | Possible niche reach. | Often low direct conversion. |

## Suggested Initial Test Ranges

These are planning ranges, not promises.

| Creator size/fit | Suggested max test |
|---|---:|
| small but high-fit | 50-150 USD |
| mid small high-fit | 150-400 USD |
| mid-tier strong demo fit | 400-1000 USD |
| large creator | usually hold unless organic interest exists |

If a single placement would consume more than 30% of the total budget, reject unless metrics already prove the channel is unusually aligned.

## 2026-05-20 Paid Creator Permission Boundary V0

Paid creator deal terms are not spend permission.

Use `paid_creator_permission_gate` in the live creator CRM as the only machine-readable field for paid creator spend:

- current rows must stay `BLOCKED_NO_PAID_CREATOR_PROOF`;
- a paid creator test can proceed only when the selected CRM row is `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED`;
- `ALLOW_PAID_CREATOR_TEST_VERIFIED` requires verified official contact route, owner-controlled inbox/access route, disclosure line, demo or Steam baseline, matching asset QA, `creator_utility_score` 3/4+, matching asset `creator_send_gate`, `send_route_class`, non-pending metadata `viewer_named_decision`, valid non-held `capture_verdict`, AB-009/KPI decision-read proof for gameplay/pressure/route-risk claims, written deliverable, capped payment, cancellation rule, and 48h result inspection owner.

Do not infer paid permission from audience fit, rate-card reply, sponsorship policy, organic reply, or a high-value creator name.

## Deal Terms Checklist

- creator/channel;
- exact deliverable;
- platform;
- publish window;
- minimum runtime;
- disclosure line;
- whether gameplay footage must be from current build;
- `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED`;
- `send_route_class`, official contact route, access/key route, and `reply_consent_provenance`;
- metadata handoff plus AB-009/KPI decision-read evidence if the brief uses gameplay, pressure, route-risk, threat, salvage failure, or first-public agency proof;
- whether talking points are optional;
- no requirement for positive opinion;
- Steam/demo link only after Official CTA Link Activation Gate V0 for public links or recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED` plus exact access-log fields for private routes;
- payment amount;
- invoice/payment method;
- cancellation;
- key/build access terms.

## Talking Points

Allowed:

- single-player-first underwater survival;
- pressure, machinery, salvage, black-water exploration;
- base as pressure vessel;
- Seed Ship anomaly;
- Steam/demo CTA only after Official CTA Link Activation Gate V0 for public links or recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED` plus exact access-log fields for private routes.
- gameplay/pressure/route-risk points only if backed by metadata `viewer_named_decision`/`capture_verdict` plus `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`.

Forbidden:

- multiplayer-scope promise;
- competitor-attack positioning;
- performance claims without proof;
- "best survival game";
- guaranteed release timing;
- false roadmap certainty.

## Paid Placement Stop Rules

Stop paid creator tests if:

- two paid tests produce no Steam movement;
- creator audience asks "what do you do?" repeatedly;
- CRM row is not `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED`;
- paid brief or creator cut claims agency/pressure proof without matching metadata handoff and AB-009/KPI decision-read field;
- comments fixate on derivative look;
- demo completion is weak;
- paid slots cost more than capsule/page fixes would.

## Creator Brief

```md
HECTON-8 creator brief

Game: HECTON-8
Pitch: single-player-first underwater survival about pressure, machinery, salvage, and black-water exploration.
Do say: pressure, machinery, salvage, base survival, Seed Ship anomaly.
Do not say: multiplayer scope, competitor-attack positioning, zero stutter, guaranteed FPS.
Disclosure: demo/key/sponsorship provided by developer.
Link: HOLD_NO_LINK - public Steam/demo only after exact destination gate plus `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`; private access only after recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED` and access-log fields.
Build notes: HOLD_BUILD_NOTES_UNVERIFIED - use exact build ID, known issues, and access route only after private/public access gates pass.
Route class: [NO_LINK_CREATOR_FEEDBACK / PUBLIC_CTA_CREATOR / PRIVATE_ACCESS_CREATOR]
Paid creator permission gate: [ALLOW_PAID_CREATOR_TEST_VERIFIED / BLOCKED_*]
Agency proof source, if claimed: HOLD_NO_AGENCY_PROOF_SOURCE - fill exact asset metadata row plus AB-009/KPI field row or remove claim.
Embargo: HOLD_NO_EMBARGO_DATE - owner must set exact date/time/timezone or explicit no-embargo state in the deal record.
```
