# Crisis And Moderation Playbook

Status: future community safety / pre-public
Public stance: single-player-first scope / proof-first public copy
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
- Move real issues to `Feedback/PLAYER_FEEDBACK_TAXONOMY_AND_TRIAGE.md`.

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

## 2026-05-19 First Public Incident Triage Gate V0

This gate is for the first screenshot, Steam page, demo, or creator preview beat. It converts public noise into a decision within 24 hours. It is not a permission to post; it only defines what to do after attention arrives.

| Signal | Threshold | Severity | First 30 minutes | 24-hour owner action | Stop condition |
|---|---:|---|---|---|---|
| Clone comparison | 5+ independent comments in one platform thread | S2 | Reply once with identity boundary; ask which visual cue caused it. | Move asset/copy to `REVISE`; update screenshot order or caption. | Do not post same asset again until revised. |
| Multiplayer-scope expectation | 3+ comments assume multiplayer or ask "how many players" | S2 | Reply once with current single-player-first public scope. | Add multiplayer-scope boundary to FAQ/pinned reply if needed. | Pull any copy that implies shared world. |
| Dark/unreadable image | 3+ comments cannot parse subject/action | S2 | Do not defend darkness as style. | Re-score asset in QA and adjust exposure/crop/caption. | Asset cannot be used as lead image. |
| AI-looking/generic asset accusation | 2+ credible comments | S2/S1 if source uncertain | State only verified capture/source facts. | Audit asset metadata and source chain. | Replace asset if source/capture proof is incomplete. |
| Performance doubt | Any claim challenged or clip visibly stutters | S1 | Remove/avoid performance language. | Collect build/hardware/capture method; update claim gate. | No FPS/zero-stutter claim until measured proof exists. |
| Press/creator misread | Creator or journalist repeats wrong feature/scope | S1 | Correct privately if possible; public correction only if needed. | Fix presskit, pitch, FAQ, Steam copy route that caused it. | Hold next outreach batch. |
| Regional wording failure | Native speaker flags bad translation or broken encoding | S2 | Thank, stop localized sends. | Send file through localization QA gate. | Region remains blocked until native-read pass. |
| Key/access scam | Unknown account requests key/access using creator name | S1 | Do not send key; verify via owner route. | Log in key policy and deny. | Escalate if impersonation repeats. |

## First-Hour Moderator Script

```text
Timestamp:
Platform/thread:
Asset/campaign ID:
Main confusion:
Repeated exact words:
Severity:
Reply posted? yes/no
Reply text:
Internal action:
Owner:
Next check time:
```

## Decision Rule

At 24 hours, every public beat gets one label:

| Label | Meaning | Next action |
|---|---|---|
| KEEP | People understand the hook and no trust issue appeared. | Continue planned sequence. |
| REVISE | Interest exists but confusion is repeated. | Change asset/copy/order before more posting. |
| KILL | The beat creates clone, AI, false-scope, or unreadability damage. | Stop the beat and document why. |

Likes are not a pass signal if comments show wrong expectations.

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
