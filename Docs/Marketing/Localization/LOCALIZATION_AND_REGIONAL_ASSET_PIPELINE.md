# Localization And Regional Asset Pipeline

Status: regional prep / review required before sending
Public stance: single-player-first scope / proof-first public copy
Runtime impact: none

## Hard Rule

Do not send, post, publish, localize a Steam page/announcement, use a regional one-pager, or count localized/regional signal unless `localization_public_permission_gate = ALLOW_LOCALIZED_PUBLIC_USE_VERIFIED` for the exact language, surface, and asset packet. Encoding repair, ASCII-safe transliteration, owner-native familiarity, draft copy, or regional lead existence is not public localization permission.

## Objective

Prepare regional messaging without machine-translated spam. Localization is not only text; it is creator fit, platform expectations, subtitle quality, Steam page readiness, and payment/access realities.

## Priority Order

| Priority | Language/region | Why | Minimum asset |
|---:|---|---|---|
| 1 | English | global baseline | Steam page + screenshots |
| 2 | Russian | user-native advantage, horror/survival audience | RU one-pager + screenshots |
| 3 | German | sim/survival/build audience | DE short pitch + base/machinery proof |
| 4 | Portuguese/Brazil | strong creator ecosystem | PT-BR short pitch + clips |
| 5 | Spanish | broad gaming/horror reach | ES short pitch + clips |
| 6 | Polish/French | useful regional niches | reviewed pitch + demo |
| 7 | Japanese/Korean | high standards, later | localized trailer/Steam page |

## Asset Types

| Asset | Localize? | When |
|---|---|---|
| Steam short description | yes for top regions | after page copy stabilizes |
| Screenshot captions | yes | after first screenshot pack |
| Trailer subtitles | yes | after final trailer timing |
| Creator pitch | yes, reviewed | before outreach |
| Press one-pager | yes for top regions | before regional press |
| Demo UI | only when product supports it | later product decision |

## Review Rule

Any localized public text must be:

- native reviewed, or
- clearly marked internal draft, or
- not sent.

Do not send mojibake. Do not send raw machine translation to creators.

## 2026-05-19 Localization QA Gate V0

Status: active gate / no public localized send without pass.

Use this before any regional pitch, caption, press note, Steam copy, or social post.

Machine gate: `localization_public_permission_gate = HOLD_LOCALIZED_PUBLIC_USE`. The only future allow value is `ALLOW_LOCALIZED_PUBLIC_USE_VERIFIED`, and it is language/surface-specific: approving RU screenshot-caption feedback does not approve RU creator outreach, RU Steam copy, German pitch, PT-BR subtitle, Spanish social post, JP/KR trailer copy, or regional press one-pager.

`ALLOW_LOCALIZED_PUBLIC_USE_VERIFIED` requires:

- exact language/region, surface, asset IDs, owner, reviewer, and review timestamp;
- encoding-clean source, no mojibake/replacement characters, and no mixed-script lookalike trap;
- native/fluent reviewer approval for naturalness and promise preservation;
- English source text already passed Promise Lint and scope/proof gates;
- every gameplay, pressure, route-risk, threat, salvage, base-failure, or first-public agency claim maps to AB-009/KPI decision-read fields;
- `public_post_permission_gate`, `public_cta_permission_gate`, `steam_announcement_permission_gate`, `steam_support_permission_gate`, `private_access_permission_gate`, `owned_audience_permission_gate`, or `discord_open_permission_gate` passes where that localized surface uses the corresponding route;
- `creator_send_gate`, CRM row mapping, `send_route_class`, and `reply_consent_provenance` pass before localized creator outreach;
- regional platform/payment/access confusion is checked or explicitly omitted from copy;
- reviewer notes and route/provenance fields are stored before any KPI or weekly-report signal is counted.

| Gate | Pass condition | Fail action |
|---|---|---|
| Encoding | No mojibake, replacement characters, broken punctuation, or mixed Cyrillic/Latin lookalikes. | Stop and repair source text before review. |
| Scope | Text says single-player-first and does not imply unsupported multiplayer scope, release date, platform, or performance proof. | Rewrite in English first, then localize again. |
| Proof | Every claim maps to a real asset, Steam page, demo, or presskit link. | Remove the claim or hold the send. |
| Native read | Native/fluent reviewer marks it natural enough for that audience. | Keep as internal draft only. |
| Regional CTA | CTA works for that region and has passed the exact destination permission gate plus destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`: Steam link, presskit, demo, or feedback ask is accessible and honest. | Use feedback-only ask or hold. |
| Access proof | Key, demo, playtest, or private-preview copy has AB-009/KPI field source, `access_route_class`, and `reply_consent_provenance` custody if it claims pressure/route-risk proof. | Remove the proof claim or hold. |
| Creator fit | The recipient format matches the asset: long-form, horror, base systems, short clip, or press. | Move lead to hold; do not force broad hype. |

### Quick Review Form

```text
Language/region:
Asset IDs:
Text reviewed:
Reviewer:
Encoding clean: yes/no
Sounds native enough: yes/no
Any added promise: yes/no
CTA usable in region: yes/no
Destination permission gate plus public CTA gate, or no-link route: yes/no
Access `access_route_class` / `reply_consent_provenance` ready if private: yes/no
AB-009/KPI agency field source if proof claim: yes/no
Decision: approve / revise / hold
Notes:
```

### Known Risk Languages

| Region | Current state | First safe use |
|---|---|---|
| RU/CIS | Owner-native review possible, but still requires asset proof, AB-009/KPI field source for agency claims, and CTA/access route custody. | First screenshot critique / regional one-pager after Regional Send Gate V0. |
| German | Draft exists, review pending. | Base/machinery proof and long-form creator pitch after AB-009/KPI field source if decision proof is used. |
| PT-BR | Draft exists, review pending. | Clip-first creator pitch after native review, CTA/access route custody, and AB-009/KPI field source if agency proof is used. |
| Spanish | Draft exists, review pending. | Clip-first creator pitch after native review, CTA/access route custody, and AB-009/KPI field source if agency proof is used. |
| Polish/French | Hold. | Demo/screenshot pack plus reviewed short pitch, route-specific class / `reply_consent_provenance`, and AB-009/KPI field source if proof claims are used. |
| Japanese/Korean | Hard hold. | Localized Steam/trailer/demo proof only after CTA/access route custody and native review. |

## Regional One-Pager Template

```md
# HECTON-8

One-line pitch:
[localized]

What it is:
[single-player-first underwater survival about pressure, machinery, salvage, black water]

What it is not:
[unsupported multiplayer-scope promise, competitor-attack pitch]

Assets:
[Steam/screens/trailer/demo]

Coverage angle:
[regional/segment fit]

Contact:
[official route]

CTA / access route:
[gated public CTA after `public_cta_permission_gate`, no-link feedback ask, or private access route logged separately]

Agency proof field:
[`what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision` if pressure/route-risk proof is claimed]
```

## Regional Feedback To Track

- translation complaints;
- region-specific genre comparisons;
- platform/payment friction;
- creator reply rate;
- Steam wishlists by country;
- video comments asking for subtitles/localization;
- confusion around multiplayer scope or public feature scope.

## Kill Criteria

Pause regional outreach if:

- localized copy is unreviewed;
- creators misread scope;
- Steam page is English-only and region expects local copy;
- no region-specific CTA is usable;
- regional CTA or access route lacks activation/provenance;
- comments focus on bad translation instead of game.
