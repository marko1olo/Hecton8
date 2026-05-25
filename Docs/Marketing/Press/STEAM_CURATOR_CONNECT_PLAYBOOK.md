# HECTON-8 Steam Curator Connect Playbook

Status: pre-Curator-Connect plan / no copies ready
Owner lane: SHINOBU_81 / Steam curator ops
Runtime impact: none

## Source Boundary

Primary source checked on 2026-05-19:

- Steam Curators and Curator Connect: https://partner.steamgames.com/doc/marketing/curators
- Steam Keys: https://partner.steamgames.com/doc/features/keys

Steamworks docs state that Curator Connect requires at least a public Coming Soon page and an uploaded playable build. Recheck before use.

## Why This Exists

Curator requests are scam-heavy. Curator Connect is safer than loose keys because it keeps delivery inside Steam and avoids email key leakage. HECTON-8 should use Curator Connect for Steam curators whenever possible.

## Hard Rules

- Do not send raw Steam keys to curator email requests when Curator Connect can be used.
- Do not send to curators before public Steam page and playable build exist.
- Do not send more than one copy unless there is a real reason.
- HECTON-8 is single-player-first; multiplayer-copy logic is not applicable.
- Do not chase follower count alone.
- Do not send to generic "we review all games" curators.
- Do not send if the first Steam screenshot set is weak, because Curator Connect exposes those.
- Do not send if the exposed set lacks one agency/decision proof asset from `PLAN-SHOT-006`, `PLAN-CLIP-001`, or `PLAN-CLIP-003` with non-pending metadata `viewer_named_decision`, valid `capture_verdict`, and AB-009/KPI viewer-named decision fields; `PLAN-SHOT-007` anomaly flavor is not a substitute.
- Do not send unless the curator tracker row has `send_permission_gate = ALLOW_CURATOR_SEND_VERIFIED`.

## Curator Connect Readiness Gate

Required:

- public Steam Coming Soon page;
- uploaded build playable by curators;
- approved store page;
- first Steam screenshot set is strong;
- first Steam screenshot set includes identity, player verb, base/machinery, and one agency/decision proof asset with non-pending metadata handoff fields plus AB-009/KPI decision-read fields;
- exposed screenshots and clips pass asset metadata claim checks;
- curator tracker row has `send_permission_gate = ALLOW_CURATOR_SEND_VERIFIED`;
- `send_route_class` is selected and `reply_consent_provenance` is still blank before a reply exists;
- tags accurately describe game;
- short personalized message;
- owner-controlled contact email ready;
- recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`, official inbox custody, disclosure, and exact access-log fields;
- QA-confirmed demo/build route.

## Selection Filters

Use Steamworks filters:

- tags: Survival, Exploration, Atmospheric, Base Building, Sci-fi, Horror, Underwater if available;
- language: English first, then RU/DE/ES/PT-BR/FR if localized materials exist;
- OS: Windows unless build support changes;
- curator focus: PC, survival, indie, horror, atmospheric, science fiction.

Manual checks:

- reviews recent enough;
- recommendations written by humans;
- games reviewed match HECTON-8 audience;
- no obvious key-resale behavior;
- off-Steam identity exists if possible;
- curator does not demand keys outside Steam.

## Scoring

Score 0-2 each.

| Criterion | 0 | 1 | 2 |
|---|---|---|---|
| Genre fit | wrong | adjacent | exact |
| Recent activity | stale | some | active |
| Review quality | low effort | mixed | useful written reviews |
| Audience relevance | broad/noisy | partial | strong |
| Scam risk | high | unclear | low |
| Language fit | mismatch | acceptable | target language |
| Screenshot compatibility | likely confused | okay | will understand identity, verb, machinery, and one decision proof recorded by AB-009/KPI fields |

Send only if 10/14 or higher.

## Send Tiers

| Tier | Count | When | Target |
|---|---:|---|---|
| Test | 10 | first Curator Connect use | High-fit curators only. |
| Batch 1 | 25 | after first test shows no issue | Survival/horror/atmospheric curators. |
| Batch 2 | 25 | after demo update | Regional/language curators. |
| Batch 3 | 40 | pre-launch | Broader PC/indie curators. |

Steam docs currently describe offers to 100 curators, max 5 copies each. For HECTON-8, default is 1 copy per curator unless justified.

## Message Template - Survival Curator

```text
HECTON-8 is a single-player deep-sea survival game focused on pressure, salvage, machinery, and base risk below the light.

Your curator page focuses on survival/atmospheric games, so the fit is the pressure loop and industrial exploration rather than multiplayer-mode expectations or scenic ocean discovery.

Current build focus: [honest build loop].
Contact: [owner-controlled email]
```

## Message Template - Horror Curator

```text
HECTON-8 is a single-player deep-sea survival game with a colder horror angle: pressure, black water, hostile visibility, machinery failure, and a buried Seed Ship anomaly.

This is not a jumpscare-first build. The current hook is sustained dread through systems and environment.

Current build focus: [honest build loop].
Contact: [owner-controlled email]
```

## Message Template - Indie/PC Curator

```text
HECTON-8 is a single-player-first PC deep-sea survival game with NASA-punk machinery, salvage routes, and pressure-driven base risk.

The reason for sending it here is the PC/indie systems angle: heavy hardware, hostile readability, and a darker survival identity.

Current build focus: [honest build loop].
Contact: [owner-controlled email]
```

## Do Not Send To

- curators demanding multiple external keys;
- curators with no relevant review history;
- curators whose last reviews are all copy-paste;
- curators focused on anime/VN/RPG only;
- curators whose page language you cannot support;
- curators that appear to be key traders;
- curators with unrelated tags only.

## Curator Log Template

```text
Date:
Curator:
Steam URL:
Tags:
Language:
Followers:
Score:
Copies sent:
Message variant:
Build/package:
Accepted: unknown / yes / no
Review posted: unknown / yes / no
Review URL:
Notes:
```

## Response Policy

If a curator asks for normal keys after receiving Curator Connect:

1. Do not send keys automatically.
2. Ask why Curator Connect is insufficient.
3. Verify identity through public curator page.
4. If suspicious, deny politely and log.

Template:

```text
Thanks for checking HECTON-8. For curator copies we are using Steam Curator Connect so the review copy stays tied to the curator account and avoids key leakage. If there is a technical issue with the Curator Connect offer, send the public curator page and details and I will check it from Steamworks.
```

## Current HECTON-8 Decision

No Curator Connect sends yet. Build the candidate list now, but do not activate from Steam page/build existence alone. Activation requires `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`, build proof, first Steam screenshot set, one agency/decision proof asset with AB-009/KPI decision-read fields, asset claim checks, `send_permission_gate = ALLOW_CURATOR_SEND_VERIFIED`, owner-controlled contact, recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`, official inbox custody, disclosure, and exact access-log fields.
