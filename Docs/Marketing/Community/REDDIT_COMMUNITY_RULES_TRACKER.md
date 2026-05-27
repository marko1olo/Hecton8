# HECTON-8 Reddit And Community Rules Tracker

Status: pre-posting tracker
Owner lane: Marketing / community operations
Runtime impact: none

## Source Boundary

Primary sitewide source:

- Reddit spam guidance: https://support.reddithelp.com/hc/en-us/articles/360043504051-What-constitutes-spam

Community rules change often and must be read same day before posting. This file is not permission to post.

## Hard Rules

- Do not astroturf.
- Do not write "I found this game" if the poster is the developer/team.
- Do not hide developer affiliation.
- Do not post the same asset/copy across multiple subreddits.
- Do not use tracking links where they are not allowed.
- Do not use wishlist, Steam, signup, Discord, demo, or presskit CTAs unless the exact destination permission gate and destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` pass; otherwise ask a no-link critique question.
- Do not argue with moderators.
- Do not ask agents to manufacture conversation.
- Do not use fake accounts to simulate interest.

## Post Type Taxonomy

| Type | When to use | CTA |
|---|---|---|
| Critique request | Before Steam page and first screenshot drop. | Ask one specific question. |
| Technical devlog | When the post teaches something real. | No wishlist push unless same-day community rules, the exact destination permission gate, and `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` all pass. |
| Screenshot Saturday | In communities with recurring promo threads. | Light CTA only if same-day community rules, the exact destination permission gate, and `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` all pass. |
| Playtest request | Only when build exists. | Signup/Steam page only if same-day community rules, the exact destination permission gate, and `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` all pass; private access uses an access log, not a public CTA. |
| Failure post | When a real problem was solved and can teach devs. | No sales push. |
| Launch/demo post | Only in promo-allowed spaces. | Steam/demo CTA only after same-day community rules, `steam_page_publish_permission_gate`, `demo_public_access_permission_gate` where demo is linked, and `public_cta_permission_gate` all pass. |

## Candidate Communities

These are not approved. Each row needs a same-day rule read.

| Community | Fit | Likely best post | Risk | Status |
|---|---|---|---|---|
| r/subnautica | High audience adjacency | Do not post unless rules and mods allow; better as listening source. | Direct competitor community; high backlash risk. | Listen-first. |
| r/Subnautica_Below_Zero | Audience adjacency | Listen-first. | Off-topic/self-promo risk. | Listen-first. |
| r/survivalgaming | High genre fit | Screenshot/clip discussion if self-promo allowed. | Promo fatigue. | Verify rules. |
| r/BaseBuildingGames | High systems fit | Base pressure/failure screenshot critique. | Needs real base proof. | Verify rules. |
| r/IndieGaming | Broad indie reach | Screenshot Saturday / devlog. | Very noisy; weak conversion. | Verify rules. |
| r/indiegames | Broad indie reach | Screenshot/clip if allowed. | Promo saturation. | Verify rules. |
| r/gamedev | Dev audience | Technical post about underwater visibility/asset pipeline. | Not buyer audience; no sales pitch. | Verify rules. |
| r/Unity3D | Tech/dev audience | Optimization/visual fake breakdown if real. | Must include concrete implementation detail. | Verify rules. |
| r/proceduralgeneration | Tech audience | World/biome generation breakdown if real. | No marketing-only post. | Verify rules. |
| r/horrorgaming | Horror audience | Pressure/dread clip if gameplay supports it. | Horror expectations may distort positioning. | Verify rules. |
| r/thalassophobia | Visual fear audience | Only if rules allow game content and asset is strong. | Many communities reject promo. | Verify rules. |
| r/ImaginaryLeviathans | Visual audience | Concept/art only if allowed and disclosed. | Art community, not game promo. | Verify rules. |
| r/pcgaming | Large PC audience | Major demo/launch only if rules allow. | Strict self-promo/news quality. | Do not post early. |
| r/Games | Large core audience | Major announcement only. | Strict rules and high scrutiny. | Do not post early. |
| r/gaming | Huge broad audience | Only if organic asset is exceptional and rules allow. | Low conversion, high backlash. | Avoid early. |
| r/playmygame | Playtest audience | Demo/playtest request. | Needs playable build. | Later. |
| r/DestroyMyGame | Brutal feedback | Demo/screenshot critique. | Must handle harsh feedback. | Later. |
| r/INAT | Dev collaboration | Not marketing. | Wrong audience. | Avoid unless hiring/collab. |
| r/Steam | Steam audience | Store/demo news only if allowed. | Self-promo risk. | Verify rules. |
| r/SteamDeck | Hardware audience | Steam Deck performance proof only. | Needs real hardware proof. | Later. |

## Same-Day Rule Read Template

```text
Date:
Community:
URL:
Rules URL:
Self-promo status:
Promo thread status:
Media allowed:
Required flair:
Account requirements:
Link policy:
Developer disclosure requirement:
Recommended post type:
Forbidden post type:
Moderator contact needed:
Decision: Listen / Comment only / Post candidate / Do not post
```

## Approved Reply Style

Use:

- direct disclosure;
- concrete system detail;
- short answers;
- no defensive tone;
- no competitor attack;
- no "wishlist please" replies unless context, same-day community rules, the exact destination permission gate, and `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` all allow it.

Example:

> Dev here. Fair criticism on readability. The target is not blue-clean ocean; we are testing heavier silhouettes, gauge light, and grime so the player can read machinery in low visibility. I am taking notes from this thread before locking the first Steam shots.

## Removal Response

If a post is removed:

1. Do not repost.
2. Do not argue publicly.
3. Read removal reason.
4. If unclear, send one polite modmail.
5. Log the community as restricted.
6. Update future post rules.

Template:

> Understood. I am the developer and was trying to ask for critique, but I may have misread the self-promo boundary. I will not repost. Is there a recurring thread or format where this kind of dev screenshot feedback is allowed?

## Astroturf Firewall

Forbidden agent actions:

- pretending to be players;
- seeding "organic" discovery posts;
- coordinated upvoting;
- fake argument threads;
- fake complaints to create drama;
- posting from multiple accounts to simulate community.

Reason: reputational damage is worse than losing a post. The 2025 public backlash around stealth marketing/astroturfing showed this is a live risk in games marketing. HECTON-8 must not build on fake discovery.

## Good Reddit Content For HECTON-8

Strong:

- "We are trying to make underwater machinery read in darkness. Which of these two screenshots is clearer?"
- "This base failure UI is too much. What would you remove first?"
- "Underwater survival games often make resource routes tedious. Here is our current route-pressure sketch; what would annoy you?"
- "We faked volumetric silt with cheap layers. Here is the before/after."

Weak:

- "Wishlist our upcoming survival game."
- "What do you think of our game?"
- "Subnautica fans, check this out."
- "We are making the next big underwater survival game."

## Community Listening Queries

Track these weekly:

- "subnautica 2 co-op desync";
- "subnautica 2 performance stutter";
- "subnautica 2 base building";
- "underwater survival game base building";
- "games like subnautica but darker";
- "survival game resource grind";
- "base building pressure";
- "deep sea horror game".

Record only actionable signals. Do not copy comments into public copy.
