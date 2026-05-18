# Crisis And Moderation Playbook

Status: future community safety / pre-public
Public stance: single-player-first / no co-op promise
Runtime impact: none

## Purpose

Prepare for predictable public failure modes before they happen. Most small projects lose trust through overpromising, defensive replies, creator-key chaos, and inconsistent moderation.

## Likely Crisis Types

| Crisis | Trigger | First response |
|---|---|---|
| "Subnautica clone" pile-on | screenshots too derivative | Ask which visual cues read derivative, revise assets. |
| Co-op demand backlash | players assume multiplayer | Restate single-player-first scope once. |
| Performance accusation | clip stutters or claim overreaches | Provide build/hardware facts or retract claim. |
| Key scam wave | fake creators request keys | Use key policy; no unverified keys. |
| Demo crash reports | public demo unstable | Acknowledge, collect repro, patch or pause outreach. |
| AI asset accusation | visuals look synthetic/generic | Explain asset/source honestly; show in-game proof when possible. |
| Regional backlash | poor translation or platform issue | Pause localized outreach and correct copy. |
| Creator negative video | build or pitch fails | Do not attack creator; extract issues and respond factually. |

## Response Rules

- Reply once with facts.
- Do not dunk on players.
- Do not mention competitors defensively.
- Do not promise fixes without owner/date.
- Do not delete criticism unless it breaks rules.
- Do not litigate design in comment chains.
- Move real issues to `PLAYER_FEEDBACK_TAXONOMY_AND_TRIAGE.md`.

## Holding Statements

### Clone Comparison

Fair comparison by setting. HECTON-8 is aimed at a different feeling: pressure, machinery, salvage, black-water exploration, and single-player industrial isolation. If a screenshot reads too close, we want to know which cue caused that.

### Co-op Requests

HECTON-8 is single-player-first. We are not promising co-op publicly. Scope honesty matters more than selling a feature that is not planned.

### Performance Claim Issue

We should not have implied performance without enough measurement. Future performance posts will include build version, hardware, settings, and capture method.

### Demo Crash

We are collecting repro details now: build version, hardware, settings, what happened before the crash, and whether a log is available. We will pause broader outreach if the demo route is unstable.

### Negative Creator Coverage

The coverage surfaced issues we need to evaluate. We are not going to argue with the creator; useful feedback will be triaged and fixed or documented.

## Internal Escalation

| Severity | Definition | Owner |
|---|---|---|
| S0 | legal/security/private leak/payment issue | lead only |
| S1 | demo-breaking crash, public false claim, key scam wave | lead + QA + ops |
| S2 | repeated confusion, bad translation, creator complaint | marketing + product |
| S3 | normal criticism, feature requests | community |

## Postmortem Template

```md
## Crisis Postmortem - YYYY-MM-DD

- Trigger:
- Source:
- Severity:
- What we said:
- What we should not repeat:
- Product issue:
- Marketing issue:
- Documentation update:
- Owner:
- Deadline:
```

## Hard No

- No fake apologies that hide the issue.
- No "players do not understand our vision."
- No "competitor fans are attacking us."
- No arguing about Subnautica.
- No blaming creators for showing bugs.
- No deleting evidence unless it violates rules.

