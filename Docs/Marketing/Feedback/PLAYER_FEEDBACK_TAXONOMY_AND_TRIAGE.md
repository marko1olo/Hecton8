# Player Feedback Taxonomy And Triage

Status: feedback operating model / pre-public
Public stance: single-player-first scope / proof-first public copy
Runtime impact: none

## Objective

Convert comments, creator coverage, demo notes, Discord posts, and Steam forum threads into actionable product and marketing decisions.

## Feedback Classes

| Class | Meaning | Owner |
|---|---|---|
| `VISUAL_READ` | Screenshot/capsule/clip clarity. | Tech art / marketing |
| `PLAYER_VERB` | Viewer cannot tell what player does. | Design / marketing |
| `AGENCY_DECISION_READ` | Viewer/player cannot name the pressure decision, tradeoff, or consequence. | Design / marketing |
| `DIFFERENTIATION` | Looks derivative or generic. | Art direction / positioning |
| `SURVIVAL_LOOP` | Resource, pressure, base, route pacing. | Design |
| `UX_READABILITY` | UI, warnings, objectives, controls. | UX |
| `PERFORMANCE` | FPS, stutter, hardware, load, crash. | Engineering / QA |
| `AUDIO_ATMOSPHERE` | Soundscape, sonar, machinery, dread. | Audio |
| `NARRATIVE_HOOK` | Seed Ship, lore, motivation, mystery. | Narrative |
| `SCOPE_CONFUSION` | Co-op, multiplayer, roadmap, Early Access. | Marketing / product |
| `COMMUNITY_SAFETY` | Abuse, spam, scams, moderation. | Community |

## Severity

| Severity | Meaning |
|---|---|
| P0 | Blocks public demo/page trust. Fix or pause campaign. |
| P1 | Repeated issue that damages conversion. Fix before next push. |
| P2 | Useful improvement, not blocking. Backlog. |
| P3 | Taste/opinion/noise. Track only if repeated. |

## Triage Table

| Date | Source | Consent/provenance | Route class | Feedback quote/summary | Class | Severity | Product issue | Marketing issue | Owner | Action |
|---|---|---|---|---|---|---|---|---|---|---|

## Feedback Provenance Gate V0

Every feedback row must state where the feedback came from and whether it was invited, public, support, creator, press, playtest, or private. Do not merge identities or contact details across routes.

| Feedback source | Allowed use | Blocker |
|---|---|---|
| Public comments/reviews/forums | Aggregate product/marketing signal. | Adding commenters to CRM/newsletter/tester lists. |
| Playtest/screening form | Product research for the agreed test route. | Reusing contact for marketing email without separate opt-in. |
| Creator/press reply | Outreach and asset-fit learning. | Treating as newsletter or playtest consent. |
| Support/bug report | Repro, support, known-issues update. | Using support email for marketing or creator outreach. |
| Discord/community post | Community signal under server rules. | Moving private server content into public copy without permission. |

## Repeat Pattern Rule

One comment is not a mandate. Three independent comments on the same issue are a signal. Ten are a problem. If creators and cold viewers independently say the same thing, treat it as a product/positioning issue, not "bad audience fit."

## Common Feedback Translation

| Raw comment | Likely meaning | Action |
|---|---|---|
| "Looks like Subnautica" | Color, diver silhouette, reef, UI, or creature framing is too familiar. | Identify visual cue and revise lead asset. |
| "Too dark" | Readability failed, not atmosphere succeeded. | Increase silhouette, contrast, navigation anchors. |
| "What do you do?" | Player verb absent. | Show tool, base, salvage, repair, route, or threat decision. |
| "What choice do I have?" / "Looks like a mood demo" | Agency decision absent; viewer sees atmosphere but not a tradeoff. | Use `AGENCY_DECISION_READ`; hold first-public or demo expansion until one repair/retreat/reroute/scan/operate/recover decision is visible. |
| "Looks like a walking sim" | Systems are not visible. | Show mechanics and consequences. |
| "I want co-op" | Audience expectation from genre/SN2. | Restate single-player-first, do not roadmap. |
| "Will it run well?" | Trust/performance concern. | Do not answer with claims; promise measured proof later. |
| "Base looks clean" | NASA-punk grime/material direction weak. | Add wear, labels, seals, utility, salt, damage. |
| "Monster game?" | Threat framing overpowered survival systems. | Rebalance copy toward pressure/machinery/salvage. |

## Creator Feedback Intake

After a creator plays or reviews:

- record video/stream link;
- timestamp issues;
- separate joke/entertainment from real issue;
- capture repeated chat questions;
- record whether Steam traffic changed;
- send thanks without arguing;
- do not request changed opinion.

## Demo Survey Questions

1. What did you think the main objective was?
2. What system did you understand first?
3. What confused you?
4. Did pressure/oxygen/power feel fair?
5. Did the base feel functional?
6. Did the world feel distinct from other underwater survival games?
7. What decision did you make under pressure, if any?
8. Did anything block you from continuing?
9. Would you wishlist after this demo?
10. What one screenshot or clip would you show someone else?

## Weekly Feedback Digest

```md
## Feedback Digest - YYYY-MM-DD

- Sources reviewed:
- Consent/provenance gaps:
- Top repeated positive:
- Top repeated negative:
- P0 issues:
- P1 issues:
- Agency decision read gaps:
- Marketing copy issue:
- Asset issue:
- Product issue:
- Recommended next test:
```
