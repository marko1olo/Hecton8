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

```text
Title: HECTON-8 Private Playtest Screening

Intro:
HECTON-8 is a single-player deep-sea survival game in development. This form is for private playtest selection only. It is not a promise of access, launch timing, co-op, or final features.

1. Email/contact route:
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

- expects co-op;
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

Use this to select testers without filling the wave with hype traffic or co-op requests.

| Signal | Points | Notes |
|---|---:|---|
| Played Subnautica/Subnautica-like survival and can explain what worked/failed. | +3 | Needed for clone-risk feedback. |
| Plays survival/base/system games and can describe friction clearly. | +3 | Needed for loop and UI feedback. |
| Likes atmosphere/horror but does not require jumpscares. | +2 | Needed for dread/readability feedback. |
| Provides full PC specs, including GPU/RAM/storage. | +2 | Required for low/mid-spec segmentation. |
| Will submit the 6-question feedback form after one session. | +3 | Non-negotiable for first external wave. |
| Can record or screenshot if asked. | +1 | Useful, not required. |
| Comfortable with unfinished build and missing content. | +2 | Reduces support friction. |
| Expects co-op/multiplayer as a core reason to test. | -5 | Reject for first wave. |
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
BASE_SYSTEM_READ
SALVAGE_TEDIUM
INVENTORY_FRICTION
MOVEMENT_WEIGHT
THREAT_READ
SEED_SHIP_CURIOSITY
LOW_SPEC_PERF
CONTROL_ACCESSIBILITY
COOP_EXPECTATION
```

If `COOP_EXPECTATION`, `CLONE_RISK`, or `DARKNESS_READABILITY` appears repeatedly, stop expanding the wave and revise page/assets/onboarding.

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
Session length:
Did you finish the route?
Where did you get stuck?
What did you think the game was about after 5 minutes?
Most interesting moment:
Most annoying moment:
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
- what not to expect;
- no co-op;
- how to report bugs.

After access:

- thank-you note;
- survey;
- no pressure for reviews;
- no reward-for-review language.

## Current HECTON-8 Decision

Build the screening forms and segment quotas now. Recruit only after the first route can be completed.
