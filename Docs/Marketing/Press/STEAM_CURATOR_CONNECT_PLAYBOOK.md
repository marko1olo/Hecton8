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
- Do not send if first three Steam screenshots are weak, because Curator Connect exposes those.

## Curator Connect Readiness Gate

Required:

- public Steam Coming Soon page;
- uploaded build playable by curators;
- approved store page;
- first three screenshots are strong;
- tags accurately describe game;
- short personalized message;
- contact email ready;
- key/access log ready;
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
| Screenshot compatibility | likely confused | okay | will understand first 3 shots |

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

Your curator page focuses on survival/atmospheric games, so the fit is the pressure loop and industrial exploration rather than co-op or cozy ocean discovery.

Current build focus: [honest build loop].
Contact: [email]
```

## Message Template - Horror Curator

```text
HECTON-8 is a single-player deep-sea survival game with a colder horror angle: pressure, black water, hostile visibility, machinery failure, and a buried Seed Ship anomaly.

This is not a jumpscare-first build. The current hook is sustained dread through systems and environment.

Current build focus: [honest build loop].
Contact: [email]
```

## Message Template - Indie/PC Curator

```text
HECTON-8 is a single-player-first PC deep-sea survival game with NASA-punk machinery, salvage routes, and pressure-driven base risk.

The reason for sending it here is the PC/indie systems angle: heavy hardware, hostile readability, and a darker survival identity.

Current build focus: [honest build loop].
Contact: [email]
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

No Curator Connect sends yet. Build the candidate list now, but do not activate until Steam page, build, screenshots, and key policy are ready.
