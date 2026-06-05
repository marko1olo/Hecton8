# Devlog And Steam News Pipeline

Status: content pipeline / pre-public
Public stance: single-player-first scope / proof-first public copy
Runtime impact: none

## Authority Boundary

Static documentation only. Public devlog, Steam news/event, community reuse, creator context, demo, wishlist, performance, platform, release, publication, support, and gameplay-proof claims require root `textes.md` for voice plus `quality.md`, `release.md`, and `platform.md` with current proof artifacts and the local permission gates.
No public post, Steam news, trailer/capsule approval, creator send, community launch, platform/release claim, demo claim, Steam event publication, newsletter reuse, or devlog publication is authorized by drafts, examples, templates, cadence rows, or plan rows in this file.

## Hard Rule

Do not publish or schedule a Steam announcement/news/event, reuse a devlog as Steam news, or count Steam announcement signal unless `steam_announcement_permission_gate = ALLOW_STEAM_ANNOUNCEMENT_VERIFIED` for the exact app/event/post. A devlog draft, Steam page existence, event template, public post approval, CTA approval, or demo existence is not Steam announcement permission.

## Objective

Use devlogs to build trust and show proof without overpromising. Devlogs should create useful assets, answer player confusion, and feed Steam/newsletter/community updates.

## Steam Announcement Permission Gate V0

Machine gate: `steam_announcement_permission_gate = HOLD_NO_STEAM_ANNOUNCEMENT`. The only future allow value is `ALLOW_STEAM_ANNOUNCEMENT_VERIFIED`, and it is post-specific: approving a Coming Soon launch announcement does not approve a demo live event, patch notes, Next Fest reminder, post-event digest, Steam news reuse, newsletter reuse, or community cross-post.

`ALLOW_STEAM_ANNOUNCEMENT_VERIFIED` requires:

- exact Steam app, event/news type, visibility setting, publish time, owner, and rollback/delete owner;
- Steamworks/admin custody and current official Steamworks event/announcement rule recheck;
- `public_post_permission_gate = ALLOW_PUBLIC_POST_VERIFIED` for the exact public announcement copy;
- `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` for every public link or button route;
- `steam_support_permission_gate = ALLOW_STEAM_SUPPORT_ROUTE_VERIFIED` if the announcement mentions support, bugs, known issues, forums, reviews, demo feedback, or performance reports;
- `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED` before any private demo/key/preview/Playtest route is referenced;
- `discord_open_permission_gate = ALLOW_DISCORD_OPEN_VERIFIED` before Discord is linked or named as an active public community route;
- `owned_audience_permission_gate = ALLOW_OWNED_AUDIENCE_VERIFIED` before newsletter/signup language is included;
- Promise Lint classification, asset IDs/build ID/source truth, non-pending metadata `viewer_named_decision`, valid non-held `capture_verdict`, and AB-009/KPI decision-read fields when gameplay/pressure/route-risk agency proof is claimed;
- route class, UTM/source fields, and no unsupported multiplayer, performance, date, roadmap, competitor-attack, fake urgency, or review-manipulation language.

## Devlog Rules

- Show real progress, not vibes.
- One topic per post.
- Include a screenshot, clip, diagram, or measured artifact when possible.
- Say what is not final.
- Do not promise unsupported multiplayer modes.
- Do not attack competitors.
- Do not claim performance without proof.
- Do not publish or reuse a devlog as Steam news, Discord copy, social copy, creator context, or presskit update when it claims gameplay, pressure, route-risk, threat, salvage failure, or first-public agency proof unless the asset metadata row has non-pending `viewer_named_decision`, valid `capture_verdict`, and the owning AB-009/KPI row records `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`.

## Content Pillars

| Pillar | Post examples |
|---|---|
| Pressure | warning language, base failure readability, depth cost. |
| Machinery | pumps, tools, hatches, power, floodlights. |
| Salvage | route planning, resource risk, return logic. |
| Base survival | habitat as pressure vessel, repair, oxygen, seals. |
| Black water | visibility, sonar, silhouettes, navigation anchors. |
| Seed Ship | anomaly effects, instruments, route corruption. |
| Performance proof | only with hardware/settings/build data. |

## Monthly Cadence

Pre-Steam:

- 1 internal devlog draft per week;
- publish only if it has real proof.

Post-Steam:

- 2 Steam announcements per month max unless demo/event active, and each one requires `steam_announcement_permission_gate = ALLOW_STEAM_ANNOUNCEMENT_VERIFIED`;
- 1 short clip per week if real footage exists;
- 1 feedback digest after major public beat.

Demo/Event:

- day 1 demo live post;
- mid-event systems post;
- final-day reminder;
- post-event feedback summary.

## Devlog Template

```md
# [Topic]

Short version:

[One paragraph explaining what changed.]

What the player should feel:

[Pressure/machinery/salvage/black-water outcome.]

What is shown:

- HOLD_ASSET_1 - exact approved asset ID/link only after metadata claim checks, QA, `viewer_named_decision`, valid `capture_verdict`, and route-specific publication gate pass.
- HOLD_ASSET_2 - exact approved asset ID/link only after metadata claim checks, QA, `viewer_named_decision`, valid `capture_verdict`, and route-specific publication gate pass.

What is not final:

- [honest caveat]

Useful feedback:

- [specific question]

Agency proof fields, if this post claims pressure gameplay or route risk:

- HOLD_AGENCY_METADATA_SOURCE - exact asset metadata `viewer_named_decision`, `capture_verdict`, and `capture_handoff_packet_id` source.
- HOLD_AGENCY_READ_SOURCE - exact AB-009/KPI `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision` source.
```

## First Ten Devlog Topics

1. What "single-player-first" means for HECTON-8.
2. Base as pressure vessel, not cozy house.
3. How pressure warnings should feel fair.
4. Salvage routes and why return planning matters.
5. Making black water readable, not just dark.
6. NASA-punk material direction: salt, grime, warning labels.
7. Seed Ship as systemic anomaly, not lore wallpaper.
8. Why machinery must look functional.
9. What we will and will not claim about performance.
10. What we need from first demo feedback.

## Steam Announcement Reuse

Every devlog should be convertible into:

- Steam announcement;
- Discord post;
- X/Bluesky thread;
- screenshot caption;
- creator context paragraph;
- presskit update.

Conversion is draft-only until the target surface's permission gate passes.
