# HECTON-8 Playtester Recruitment And Screening Plan

Status: pre-playtest plan
Owner lane: SHINOBU_81 / player research
Runtime impact: none

## Purpose

The first playtesters must not be random hype traffic. They need to expose whether the survival loop, pressure identity, UI, and first route work.

## Tester Types

| Type | Count | Use |
|---|---:|---|
| Cold survival players | 10-20 | Can the genre hook land? |
| Subnautica-adjacent players | 10-20 | Do they see differentiation or clone risk? |
| Horror/atmosphere players | 5-10 | Does dread work without jumpscare spam? |
| Systems/base players | 5-10 | Does machinery/base loop read? |
| Low-spec players | 5-10 | Performance and readability pain. |
| Accessibility/settings-sensitive players | 3-5 | Controls, readability, motion, text. |

## Recruitment Sources

- email waitlist;
- Discord after proof assets;
- trusted creator communities only with permission;
- Steam Playtest signup;
- small direct invites from verified leads;
- personal network with disclosure of bias.

Do not recruit via fake scarcity posts.

## Recruitment Permission Gate V0

Do not treat a visible email, Discord user, Steam commenter, or creator CRM row as consent to recruit.

| Source | Allowed only if | Blocker |
|---|---|---|
| Email waitlist | The person opted into the matching playtest/waitlist mode and `owned_audience_permission_gate = ALLOW_OWNED_AUDIENCE_VERIFIED` for that mode. | Imported CRM/contact rows, scraped emails, vague newsletter consent, or a held owned-audience gate. |
| Discord/community | The server/channel rules permit tester recruitment, `discord_open_permission_gate = ALLOW_DISCORD_OPEN_VERIFIED` for any public invite/server route, and the invite points to an owned-audience or support/signup route with its exact destination gate plus `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`. | DM recruitment, mod-disallowed posts, public invite before Discord custody/open gate, or signup link from a held destination gate. |
| Steam Playtest signup | Steam page/Playtest access has `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`, `demo_public_access_permission_gate = ALLOW_PUBLIC_DEMO_ACCESS_VERIFIED` for public signup/tranche, destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` for public links, and route class is recorded. | Treating Playtest signup as newsletter, creator, press consent, or public access approval from build existence. |
| Verified lead/direct invite | The relationship or public route makes a one-off invite appropriate. | Mass direct messages or creator/press rows with no tester opt-in. |
| Personal network | Bias is disclosed and feedback is tagged as biased. | Counting friends/family as cold read or external validation. |

Every recruited tester row needs source, consent class, route class, segment, and feedback obligation. Do not merge tester contacts into newsletter, creator CRM, press list, or Discord roles unless they explicitly opt into that separate route.

If recruitment copy claims gameplay, pressure, route risk, threat, salvage, base failure, or first-public agency proof from an asset, it also needs a non-pending asset metadata `viewer_named_decision`, a valid non-held `capture_verdict`, and an AB-009/KPI field source: `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`. Playtesters can generate new product feedback, but recruitment cannot use unmeasured marketing proof or pending/held metadata as the hook.

## Screening Questions

Use 8 questions max:

1. What survival/exploration games do you play?
2. Have you played Subnautica or similar underwater survival games?
3. What PC specs do you have?
4. Are you willing to send bug/feedback forms?
5. Do you prefer exploration, base building, horror, or systems?
6. What language do you prefer?
7. Can you record gameplay or screenshots if asked?
8. Are you okay with an unfinished build?

## 2026-05-19 Screening Form V1

Use this exact structure for the first private form. Keep it short. The goal is segment quality, not hype collection.

Private screening forms are not public CTA destinations. Do not place a screening form in a social bio, trailer end card, showcase CTA, or public presskit unless `owned_audience_permission_gate = ALLOW_OWNED_AUDIENCE_VERIFIED` and `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` both authorize the signup route. The default first-wave form is invite-only or low-visibility direct recruitment.

```text
Title: HECTON-8 Private Playtest Screening

Intro:
HECTON-8 is a single-player deep-sea survival game in development. This form is for private playtest selection only. It is not a promise of access, launch timing, multiplayer-scope, or final features.

1. Email/contact route:
1a. Consent source: waitlist / direct invite / Steam Playtest / community permission / personal network
1b. `route_class` if public signup after CTA activation; `access_route_class` if private screening or Steam Playtest
1b-1. If access is private: record `verified_contact_route`, `access_route_class`, and later `reply_status_after_send`
1c. `reply_consent_provenance`: tester feedback route only unless separate opt-in exists
2. Preferred language:
3. Country/time zone:
4. PC specs: CPU / GPU / RAM / storage / monitor refresh rate:
5. Which games have you played for more than 10 hours? [Subnautica, Below Zero, Barotrauma, The Long Dark, The Forest/Sons, Pacific Drive, Raft, Satisfactory, Space Engineers, Dredge, horror games, other]
6. Pick your strongest player type: exploration / survival systems / base building / horror atmosphere / vehicles / optimization-low-spec / accessibility-feedback.
7. What makes you quit a survival game fastest?
8. Are you willing to send bug reports and a 6-question feedback form after one session?
9. Can you record gameplay or screenshots if asked? yes / no
10. Are you comfortable testing an unfinished build with missing content and known bugs? yes / no
```

Auto-reject for first wave:

- expects multiplayer/co-op as the core reason to test;
- wants only free early access with no feedback;
- refuses bug/feedback form;
- cannot share hardware specs;
- is mainly looking for streaming content before public permission.

Priority for first 25 external testers:

- 5 Subnautica-adjacent players who can articulate clone risk;
- 5 systems/base players;
- 5 atmosphere/horror players;
- 5 low-spec or mid-spec players;
- 5 general cold survival players.

## 2026-05-19 Playtest Screening Score V0

Status: form-ready / recruitment blocked until first route is playable.

Use this to select testers without filling the wave with hype traffic or unsupported multiplayer-scope requests.

| Signal | Points | Notes |
|---|---:|---|
| Played Subnautica/Subnautica-like survival and can explain what worked/failed. | +3 | Needed for clone-risk feedback. |
| Plays survival/base/system games and can describe friction clearly. | +3 | Needed for loop and UI feedback. |
| Likes atmosphere/horror but does not require jumpscares. | +2 | Needed for dread/readability feedback. |
| Provides full PC specs, including GPU/RAM/storage. | +2 | Required for low/mid-spec segmentation. |
| Will submit the 6-question feedback form after one session. | +3 | Non-negotiable for first external wave. |
| Can record or screenshot if asked. | +1 | Useful, not required. |
| Comfortable with unfinished build and missing content. | +2 | Reduces support friction. |
| Expects multiplayer/co-op as a core reason to test. | -5 | Reject for first wave. |
| Wants only free early access/content and refuses feedback. | -5 | Reject for first wave. |
| Cannot share hardware specs. | -3 | Hold unless non-performance segment is needed. |
| Mainly wants streamable public content before permission. | -3 | Hold until public demo. |

First external wave:

- 9+ points: priority candidate if segment quota needs them.
- 6-8 points: reserve.
- below 6: hold.
- any hard reject: do not invite to first wave.

### First Wave Quota Sheet

| Slot | Segment | Count | Required minimum |
|---|---|---:|---|
| S1 | Subnautica-adjacent clone-risk readers | 5 | Must answer "what makes this not a clone?" after play. |
| S2 | Survival/base/system players | 5 | Must comment on pressure, salvage, base, inventory, route cost. |
| S3 | Horror/atmosphere players | 5 | Must comment on dread/readability without asking for jump scares. |
| S4 | Low/mid-spec players | 5 | Must provide hardware/settings and readability feedback. |
| S5 | Cold survival players | 5 | Must be unfamiliar enough to test first-five-minute clarity. |

### Feedback Tags

Use these tags in feedback logs:

```text
CLARITY_PLAYER_VERB
CLONE_RISK
DARKNESS_READABILITY
PRESSURE_SYSTEM_READ
AGENCY_DECISION_READ
BASE_SYSTEM_READ
SALVAGE_TEDIUM
INVENTORY_FRICTION
MOVEMENT_WEIGHT
THREAT_READ
SEED_SHIP_CURIOSITY
LOW_SPEC_PERF
CONTROL_ACCESSIBILITY
MULTIPLAYER_SCOPE_EXPECTATION
```

If `MULTIPLAYER_SCOPE_EXPECTATION`, `CLONE_RISK`, `DARKNESS_READABILITY`, or missing `AGENCY_DECISION_READ` appears repeatedly, stop expanding the wave and revise page/assets/onboarding.

## Tester Wave Plan

| Wave | Size | Build risk | Goal |
|---|---:|---|---|
| Internal smoke | 3-5 | High | Does it run and complete first route? |
| Trusted brutal testers | 10 | Medium/high | Find obvious friction. |
| Targeted segment test | 25 | Medium | Validate hook/loop. |
| Steam Playtest small wave | 100-300 | Medium/low | Measure broader confusion/performance. |
| Public demo | open | Low | Marketing conversion and feedback. |

## Feedback Form

```text
Consent/source:
route_class if public / access_route_class if private:
verified_contact_route if private:
access_route_class if private:
reply_status_after_send:
reply_consent_provenance:
Session length:
Did you finish the route?
Where did you get stuck?
What did you think the game was about after 5 minutes?
Most interesting moment:
Most annoying moment:
What decision did you make under pressure, if any?
If this answer is used as agency proof, record the owner-local field as `agency_decision_read` or `cold_read_agency_decision`; if it is tied to a screenshot/clip claim, also bind it to the asset metadata `viewer_named_decision` and `capture_verdict`. Do not treat the raw quote as public proof by itself.
Did resource collection feel tense or tedious?
Did pressure/machinery feel important?
Would you wishlist/follow?
Hardware/settings:
```

## Tester Communication

Before access:

- current build state;
- known issues;
- what feedback is needed;
- `verified_contact_route`, `access_route_class`, `reply_status_after_send`, `reply_consent_provenance`, and whether feedback may be quoted, summarized, or kept internal;
- what not to expect;
- multiplayer-scope boundary;
- how to report bugs.

After access:

- thank-you note;
- survey;
- no pressure for reviews;
- no reward-for-review language.

## Current HECTON-8 Decision

Build the screening forms and segment quotas now. Recruit only after the first route can be completed.
