# Press Kit And Media Plan

## Authority Boundary

Static documentation only. Current source, active architecture contracts, fresh proof artifacts, and official platform rules override dated claims in this file. No runtime, profiler, memory, render, platform, public-page, or ship-readiness proof is implied by this file alone.

Status: shell / do not pitch broadly before proof assets

## Purpose

The press kit exists so creators and journalists can understand the game in 30 seconds without asking for basics. It is not a hype shrine.

## Required Press Kit Files

- factsheet packet
- short-description packet
- long-description packet
- `screenshots/`
- `trailer/`
- `logo/`
- `capsules/`
- contact packet
- key-request policy packet
- feature-boundaries packet
- performance-claims packet

## 2026-05-19 Press Kit Build Ticket V0

Status: not buildable yet / asset-gated / do not send.

The press kit becomes buildable only after the Steam page assembly and screenshot campaign produce a `KEEP` decision. That decision must include identity, player verb, base/machinery, and one agency/decision proof asset from `PLAN-SHOT-006`, `PLAN-CLIP-001`, or `PLAN-CLIP-003` with AB-009/KPI viewer-named decision fields; `PLAN-SHOT-007` anomaly flavor cannot substitute.

| Press kit item | Source now | Required proof before publish | Reject condition |
|---|---|---|---|
| Factsheet packet | Factsheet fields below | Contact, Steam URL, presskit URL, build/demo state are real. | Any unresolved public-facing placeholder. |
| Short-description packet | `Steam/STORE_PAGE_COPY_MATRIX.md` | Candidate selected by cold read and no unsupported multiplayer/performance/large-world claim. | Viewers cannot name player verb. |
| Long-description packet | Steam copy matrix + feature boundaries | Only current-build or clearly scoped public facts. | Future roadmap sold as current feature. |
| `screenshots/` | `PLAN-SHOT-001` through `PLAN-SHOT-007` | Public shots pass QA, have build IDs, and the lead set includes one readable player decision recorded in AB-009/KPI fields. | AI/concept-looking, unreadable, generic clone frame, or passive anomaly-only set. |
| `trailer/` | `PLAN-CLIP-001` through `PLAN-CLIP-004` | First 3 seconds show player verb or system problem; one beat shows a player choice under pressure. | Beauty footage with no gameplay read or no decision pressure. |
| `logo/` | Brand asset source | Readable mark on dark/light backgrounds. | Tiny-size unreadable. |
| `capsules/` | `PLAN-CAPSULE-001` winner | AB-002/cold-read winner exists. | Title unreadable or one-note blue/black. |
| Contact packet | Owner-controlled email only | Project email and response owner exist. | Personal/orphan email or no owner. |
| Key-request policy packet | Review-key protocol | Current Steamworks key rules rechecked. | Implies keys are available before approval. |
| Feature-boundaries packet | FAQ/roadmap policy | Multiplayer-scope boundary, performance proof boundary, and unsupported-scope boundary. | Any vague "planned soon" promise. |
| Performance-claims packet | Profiler/hardware proof only | Build/settings/hardware/frame-time context exists. | Empty "runs well" language. |

### Press Kit Publish Gate

Machine gate:

- current value: `press_release_permission_gate = HOLD_NO_PRESS_RELEASE_PUBLICATION`;
- future allow value: `ALLOW_PRESS_RELEASE_PUBLISH_VERIFIED`;
- applies to presskit publish, "presskit is live" announcements, public media one-pagers, press release reuse, and presskit links placed into Steam news, social posts, emails, site pages, or distribution copy.

Do not publish or link a press kit until:

- Campaign 01 is `KEEP`;
- Steam page launch gate is passed or the kit clearly says Steam URL is not live;
- `press_release_permission_gate = ALLOW_PRESS_RELEASE_PUBLISH_VERIFIED` for the exact surface;
- `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` for every public presskit, Steam, demo, support, Discord, or signup link;
- at least 6 screenshots have asset metadata, QA score, build ID, and claim-check fields passed;
- at least one public media asset proves a readable player decision under threat, leak, route cost, sonar pressure, or salvage failure, with non-pending `viewer_named_decision`, `capture_verdict = KEEP_TESTING` or stronger campaign `KEEP`, and `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision` recorded where the proof comes from first-page assets;
- contact route is owner-controlled through `official_inbox_custody_gate = ALLOW_OFFICIAL_INBOX_USE_VERIFIED`;
- private access/key policy is included even if no keys exist;
- every file says single-player-first, passes Promise Lint, and avoids competitor-war language.

## Factsheet Fields

| Field | Current Draft |
|---|---|
| Title | HECTON-8 |
| Genre | Single-player-first underwater survival / exploration / base systems |
| Tone | NASA-punk / Deep Sea Noir |
| Platform | PC / Steam target unless changed |
| Engine | Unity 6 URP |
| Core hook | Pressure, salvage, machinery, and isolation below the light |
| Multiplayer modes | Outside current public scope |
| Current state | In development |
| Contact | HOLD_NO_PROJECT_INBOX_CUSTODY - official project email only after `official_inbox_custody_gate = ALLOW_OFFICIAL_INBOX_USE_VERIFIED`. |
| Website | HOLD_NO_PUBLIC_SITE_URL - public site only after owner custody, no-link/CTA gate, and current factsheet state pass. |
| Steam | HOLD_NO_STEAM_PAGE_PUBLICATION - link only after `steam_page_publish_permission_gate` and destination-specific `public_cta_permission_gate`. |
| Press assets | HOLD_NO_PRESS_RELEASE_PUBLICATION - public presskit/assets only after `press_release_permission_gate` and destination-specific `public_cta_permission_gate`. |

## Press Angles

Use:

- "A darker, industrial take on underwater survival."
- "Survival where pressure, machinery, and oxygen are readable systems."
- "Deep-sea noir instead of bright alien wonder."
- "A single-player-first survival game with proof-first public scope."
- "Seed Ship anomaly as systemic corruption, not cutscene lore."

Avoid:

- "Subnautica killer."
- "Better than Subnautica."
- "100km multiplayer."
- "Zero-stutter" unless measured.
- "Realistic ocean simulation" unless scoped and proven.

## Key Request Policy

Do not distribute keys from demo/build existence alone.

When keys exist:

- issue small batches only after stable demo/review-build proof, the exact recipient or batch has `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`, official inbox custody, access-log fields, disclosure, and the relevant press/creator/curator send gate;
- tag every key batch by purpose;
- do not give keys to gray-market request spam;
- require disclosure for paid/sponsored/free-key content where applicable;
- maintain a denylist of suspicious domains;
- do not pay creators through key value.

Relevant public rules/guidance:

- Steam keys: https://partner.steamgames.com/doc/features/keys
- FTC endorsement guidance: https://www.ftc.gov/business-guidance/resources/ftcs-endorsement-guides-what-people-are-asking
- YouTube paid promotions policy: https://support.google.com/youtube/answer/10588440?hl=en
- TikTok branded content policy: https://www.tiktok.com/legal/page/global/bc-policy/en

R19 key-policy boundary: `KEY_POLICY_PENDING`. For pre-release press/influencer access, use Steam Release State Override or other applicable Steam key types only after current Steamworks limits/type rules are rechecked. Never sell Release State Override, developer-comp, press, beta, or influencer keys.

## Press Email Skeleton

Subject: HECTON-8 - single-player deep-sea noir survival

Hi [Name],

HECTON-8 is a single-player-first underwater survival game about pressure, salvage, heavy machinery, and industrial deep-sea isolation.

The short version: survival below the light where every base, machine, and route is under pressure. The public angle stays proof-first and competitor-neutral.

Useful assets:

- Steam page: [official Steam URL only after `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED` and `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` for that URL]
- trailer: [asset ID/link only after asset metadata claim checks, QA, press/creator claim checks where applicable, and destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` for public links]
- screenshots: [asset IDs/links only after asset metadata claim checks, QA, and public presskit/CTA gates where linked publicly]
- press kit: [public link only after `press_release_permission_gate = ALLOW_PRESS_RELEASE_PUBLISH_VERIFIED` and destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`]

Best angle for your audience: [one sentence customized to outlet].

Thanks,
[Name]

## Embargo Discipline

Do not use embargoes until the build is stable and the assets are final enough. Broken embargo communication is worse than no embargo.

Use embargoes only for:

- demo release;
- Next Fest preview;
- Early Access launch;
- major trailer.

## Media FAQ

Q: Is HECTON-8 multiplayer?

A: HECTON-8 is single-player-first. Additional modes are outside current public scope unless they become real in the build.

Q: Is this trying to be Subnautica?

A: No. HECTON-8 shares underwater survival adjacency, but the identity is industrial deep-sea noir: pressure, machinery, salvage, corrosion, and system failure.

Q: Can you claim low-end performance?

A: Only after measured proof exists. Until then, public language is "designed with scalable quality in mind", not a performance guarantee.
