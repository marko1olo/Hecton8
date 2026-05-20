# HECTON-8 Press Release And Email Templates

Status: draft bank / not send ready
Owner lane: SHINOBU_81 / press operations
Runtime impact: none

## Public Release Permission Gate

Current machine gate: `press_release_permission_gate = HOLD_NO_PRESS_RELEASE_PUBLICATION`.

Future allow value: `ALLOW_PRESS_RELEASE_PUBLISH_VERIFIED`.

This gate applies to any public press release, public presskit announcement, site/media one-pager release copy, Steam news reuse, email press release, wire/distribution copy, embargo announcement, or public "presskit is live" beat.

Do not infer release permission from a template, presskit draft, Steam page existence, public CTA approval, public post approval, press tracker `send_permission_gate`, or localization approval alone.

`ALLOW_PRESS_RELEASE_PUBLISH_VERIFIED` requires:

- exact surface and owner: press release, presskit publish, site one-pager, Steam news, targeted email, social/blog, wire, or localized/regional release;
- real asset IDs, build/source truth, Campaign 01 `KEEP`, and presskit minimum packet or an explicit no-presskit/no-Steam state;
- `official_inbox_custody_gate = ALLOW_OFFICIAL_INBOX_USE_VERIFIED` for contact;
- `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` for every public Steam, presskit, demo, site, signup, support, or Discord link;
- `send_permission_gate = ALLOW_PRESS_SEND_VERIFIED` for every targeted press email;
- `steam_announcement_permission_gate = ALLOW_STEAM_ANNOUNCEMENT_VERIFIED` before Steam news/event reuse;
- `public_post_permission_gate = ALLOW_PUBLIC_POST_VERIFIED` before social/blog publication;
- `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED` before preview/demo/key access is referenced;
- `steam_support_permission_gate = ALLOW_STEAM_SUPPORT_ROUTE_VERIFIED` when support, forum, known-issues, or review routes are named;
- `localization_public_permission_gate = ALLOW_LOCALIZED_PUBLIC_USE_VERIFIED` for every localized/regional release surface;
- Promise Lint, asset metadata claim checks, AB-009/KPI decision-read fields for gameplay/pressure/route-risk claims, route/UTM/source fields, and no unsupported multiplayer/performance/date/competitor-war claims;
- wire/distribution and embargo copy must name owner, cost, rollback/retraction route, and publish time before use.

## Boundary

These templates are scaffolds. Do not publish or send until:

  `press_release_permission_gate = ALLOW_PRESS_RELEASE_PUBLISH_VERIFIED` for the exact public or press surface;
  Official CTA Link Activation Gate V0 passes for Steam/presskit public links, or private access route is logged;
  screenshots are real in game captures;
  build/demo status is true;
  contact email is owner-controlled and logged;
  asset metadata claim checks pass for any referenced screenshot, footage, Steam link, demo, or presskit, and link/access route gates pass;
  any gameplay, pressure, route-risk, threat, salvage failure, or first-public proof claim has AB-009/KPI decision-read evidence: `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`;
  embargo/key terms are documented.

## One Line Boilerplate

HECTON-8 is a single player first NASA punk deep sea survival game about pressure, salvage, machinery, and the cost of staying alive below the light.

## Short Factsheet Block

```text
Title: HECTON-8
Genre: Single player deep sea survival / base systems / sci fi
Developer: [Studio/team name]
Platform: PC / Steam [confirm]
Release window: [Only if real]
Demo: [Yes/No/TBD]
Steam: [approved Steam URL after CTA activation packet]
Presskit: [approved presskit URL after CTA activation packet]
Contact: [owner-controlled email]
```

## Press Email   First Reveal

Subject options:

  HECTON-8 reveal: single player deep sea survival under pressure
  NASA punk deep sea survival: HECTON-8 first screenshots
  HECTON-8: pressure, salvage, and machinery below the light

Body:

```text
Hi [Name],

I am reaching out with the first in game look at HECTON-8, a single player first deep sea survival game built around pressure, salvage, industrial machinery, and a buried Seed Ship anomaly.

The angle is colder NASA punk survival: bases that feel like hardware, salvage routes that matter, and an ocean that reads as hostile before it reads as pretty.

Presskit:
[approved presskit URL after CTA activation packet]

Steam:
[approved Steam URL after CTA activation packet]

If this fits your PC/indie/survival coverage, I can send a short preview build or a cleaner asset pack when it is ready.

Best,
[Name]
```

## Press Email Template   Demo Available After Gate

Use this template only after the demo build, access method, Steam URL, presskit URL, and honest exclusions are live and logged. Before that, keep it as draft copy only.

Subject options:

  HECTON-8 demo available for preview
  Preview code: industrial deep sea survival HECTON-8
  HECTON-8 demo: pressure systems and salvage loop

Body:

```text
Hi [Name],

HECTON-8 has a preview demo available for press/creator review as of [date/proof link]. It is a single player deep sea survival build focused on [current demo loop: salvage/base pressure/vehicle route/etc.].

What is in the demo:
  [Feature 1]
  [Feature 2]
  [Feature 3]

What is outside the demo scope:
  unsupported multiplayer modes;
  [other honest exclusions].

Presskit:
[approved presskit URL after CTA activation packet]

Steam:
[approved Steam URL after CTA activation packet]

Preview access:
[Key/access method]

There is no embargo unless noted here: [embargo status].

Best,
[Name]
```

## Press Release Skeleton   Steam Page Launch

```text
FOR IMMEDIATE RELEASE

HECTON-8 Opens Its Steam Page With A Single Player Deep Sea Survival Vision

[City, Date]   [Studio] has opened the Steam page for HECTON-8, a single player first NASA punk deep sea survival game about pressure, salvage, machinery, and the cost of staying alive below the light.

HECTON-8 puts players in [short player role if finalized], where survival depends on [current proven systems]. The game focuses on industrial atmosphere, hostile visibility, mechanical risk, and the mystery of a buried Seed Ship anomaly.

"[Short quote from developer: concrete, not hype.]"

Current public materials include [screenshots/trailer/demo status]. The team is currently focused on [honest current production focus].

Steam:
[approved Steam URL after CTA activation packet]

Presskit:
[approved presskit URL after CTA activation packet]

About HECTON-8:
[boilerplate]

Press contact:
[email]
```

## Press Release Skeleton   Demo / Festival

Use this skeleton only after the public demo or festival page is live. Replace every bracketed field with current proof before release.

```text
FOR IMMEDIATE RELEASE

HECTON-8 Demo Lets Players Test Survival Below The Light

[City, Date]   [Studio] releases a playable demo for HECTON-8 on Steam, giving players a first look at [current demo route].

The demo focuses on [3 real systems]. It does not include [honest exclusions].

[Quote: what feedback the team wants.]

Steam demo:
[approved demo URL after CTA activation packet]

Presskit:
[approved presskit URL after CTA activation packet]
```

## Quote Bank

Use only if true.

  "HECTON-8 is about pressure, machinery, and what breaks when the ocean wins."
  "The first public materials focus on real in game readability: can you understand the route, the danger, and the machine without a paragraph of explanation?"
  "We are treating performance claims as receipts, not slogans. If we talk about frame time, we will show the build and hardware context."
  "HECTON-8 is single player first. Public scope stays inside what the current build can prove."

## Forbidden Press Lines

  "Subnautica killer"
  "better than Subnautica"
  "zero stutter"
  "100km multiplayer"
  "fully simulated ocean"
  "infinite world"
  "AAA quality" without context
  "the most realistic underwater survival game"

## Media Angle Map

| Outlet type | Best angle |
|---|---|
| PC gaming press | Steam page/demo, survival systems, visual identity. |
| Indie press | small team, hard positioning, production honesty. |
| Horror press | pressure, hostile visibility, non combat dread. |
| Survival/crafting press | base failure, salvage, resource loop. |
| Tech/dev press | optimization/proof receipts, visual fake first method. |
| Regional press | localized one pager and regional creator clips. |

Gameplay/pressure/route-risk press angles are locked until the presskit/source asset can cite AB-009/KPI decision-read fields. If the field is missing, use identity/process/news framing only, or do not send.

## Follow Up Email

```text
Hi [Name],

Quick follow up on HECTON-8. Since my first note, the most useful angle may be [one specific angle based on their outlet].

Assets:
[approved asset URL after CTA activation packet]

Steam:
[approved Steam URL after CTA activation packet]

If this is not a fit, no problem. I will avoid further follow ups.

Best,
[Name]
```

One follow up maximum unless they reply.
