# Localization And Regional Asset Pipeline

Status: regional prep / review required before sending
Public stance: single-player-first / no co-op promise
Runtime impact: none

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

| Gate | Pass condition | Fail action |
|---|---|---|
| Encoding | No mojibake, replacement characters, broken punctuation, or mixed Cyrillic/Latin lookalikes. | Stop and repair source text before review. |
| Scope | Text says single-player-first and does not imply co-op, multiplayer, release date, platform, or performance proof. | Rewrite in English first, then localize again. |
| Proof | Every claim maps to a real asset, Steam page, demo, or presskit link. | Remove the claim or hold the send. |
| Native read | Native/fluent reviewer marks it natural enough for that audience. | Keep as internal draft only. |
| Regional CTA | CTA works for that region: Steam link, presskit, demo, or feedback ask is accessible and honest. | Use feedback-only ask or hold. |
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
Decision: approve / revise / hold
Notes:
```

### Known Risk Languages

| Region | Current state | First safe use |
|---|---|---|
| RU/CIS | Owner-native review possible, but still requires asset proof. | First screenshot critique / regional one-pager. |
| German | Draft exists, review pending. | Base/machinery proof and long-form creator pitch. |
| PT-BR | Draft exists, review pending. | Clip-first creator pitch after native review. |
| Spanish | Draft exists, review pending. | Clip-first creator pitch after native review. |
| Polish/French | Hold. | Demo/screenshot pack plus reviewed short pitch. |
| Japanese/Korean | Hard hold. | Localized Steam/trailer/demo proof only. |

## Regional One-Pager Template

```md
# HECTON-8

One-line pitch:
[localized]

What it is:
[single-player-first underwater survival about pressure, machinery, salvage, black water]

What it is not:
[no co-op promise, not a Subnautica killer pitch]

Assets:
[Steam/screens/trailer/demo]

Coverage angle:
[regional/segment fit]

Contact:
[official route]
```

## Regional Feedback To Track

- translation complaints;
- region-specific genre comparisons;
- platform/payment friction;
- creator reply rate;
- Steam wishlists by country;
- video comments asking for subtitles/localization;
- confusion around co-op or scope.

## Kill Criteria

Pause regional outreach if:

- localized copy is unreviewed;
- creators misread scope;
- Steam page is English-only and region expects local copy;
- no region-specific CTA is usable;
- comments focus on bad translation instead of game.
