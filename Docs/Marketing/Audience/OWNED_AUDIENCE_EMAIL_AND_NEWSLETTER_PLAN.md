# HECTON-8 Owned Audience, Email, And Newsletter Plan

Status: pre-list operating plan
Owner lane: SHINOBU_81 / owned audience
Runtime impact: none

## Purpose

Steam wishlists matter, but HECTON-8 should not depend only on Steam algorithms, creator replies, Reddit luck, or paid reach. An owned mailing list gives the project a direct channel for demo access, playtest waves, launch reminders, and feedback.

## Hard Rules

- Do not buy email lists.
- Do not scrape emails.
- Do not add creators/press to newsletter without opt-in.
- Do not spam weekly empty updates.
- Do not promise co-op, release dates, or performance.
- Every signup form must say what people are signing up for.
- Every email must have an unsubscribe path.

## Signup Offers

Use only honest offers:

| Offer | When to use | Copy |
|---|---|---|
| Demo alert | Before demo | "Get one email when the HECTON-8 demo/playtest opens." |
| Playtest waitlist | Before Steam Playtest/private playtest | "Join the playtest waitlist for single-player deep-sea survival feedback." |
| Devlog digest | After screenshots | "Occasional devlog updates on pressure, salvage, machinery, and the Seed Ship." |
| Press/creator list | After presskit exists | Separate list, not general newsletter. |

## Signup Form Fields

Minimum:

- email;
- consent checkbox;
- preferred platform/language optional;
- playtest interest optional.

Do not ask for long surveys on first signup.

## 2026-05-19 Owned Audience Signup Gate V0

Status: draft-only / no signup push before real value exists.

Use an email list only when there is a concrete reason for a player to hear from HECTON-8. Do not build a dead list from vague hype.

### Signup Modes

| Mode | When allowed | Fields | Promise | Stop condition |
|---|---|---|---|---|
| `DEMO_ALERT` | Steam page or demo/playtest is close enough to define honestly. | Email, consent, preferred language. | One email when demo/playtest opens. | Stop if demo scope/date is unknown. |
| `PLAYTEST_WAITLIST` | First route is playable internally and screening score is active. | Email, consent, language, region, hardware opt-in, segment interest. | Possible invite, not guaranteed access. | Stop if build cannot support external testers. |
| `DEVLOG_DIGEST` | First screenshot pack has passed QA. | Email, consent, preferred language. | Occasional major updates only. | Stop if updates would be filler. |
| `PRESS_CREATOR_CONTACT` | Presskit exists. | Work email/contact, outlet/channel, consent. | Press/creator updates only. | Stop if presskit/contact policy is not ready. |

### Signup Copy Blocks

#### Demo Alert

```text
Get one email when the HECTON-8 demo or playtest opens.

HECTON-8 is single-player deep-sea survival about pressure, salvage, machinery, and black water. No co-op promise, no weekly filler.
```

#### Playtest Waitlist

```text
Apply for future HECTON-8 playtest waves.

This is for feedback on a single-player survival build: clarity, pressure systems, salvage, base machinery, controls, and performance context. Access is not guaranteed.
```

#### Devlog Digest

```text
Occasional HECTON-8 development updates when there is something real to show: screenshots, Steam page, demo/playtest, or major systems notes.
```

### List Hygiene

- Do not import creator/press CRM rows into the audience list.
- Do not add anyone without explicit consent.
- Segment by signup mode at collection time.
- Send a confirmation/welcome email only.
- If no meaningful update exists for 60 days, send nothing.
- If unsubscribe/complaint rate rises, pause and audit copy/source.

## Segments

| Segment | Use |
|---|---|
| Players - general | Demo, Steam page, major updates. |
| Playtest candidates | Controlled access and surveys. |
| Creators | Asset/build announcements, not newsletter spam. |
| Press | Presskit/demo beats only. |
| Regional | Localized demo/Steam alerts. |
| Technical/dev audience | Devlog/optimization/process posts only if proof exists. |

## Email Cadence

Before screenshots:

- no regular newsletter unless there is real progress;
- one signup confirmation only.

After screenshots:

- monthly at most;
- only if asset/update is meaningful.

Before demo:

- one announcement;
- one reminder near demo;
- one feedback request after demo.

After launch/EA:

- patch/update-driven, not calendar spam.

## Welcome Email

Subject:

```text
You are on the HECTON-8 list
```

Body:

```text
Thanks for signing up.

HECTON-8 is a single-player-first deep-sea survival game about pressure, salvage, machinery, and the Seed Ship anomaly.

This list is for major updates only: first screenshots, Steam page, demo/playtest access, and launch news. No co-op promises, no fake performance claims, no weekly filler.

Steam: [URL when live]
```

## Demo Alert Email

Subject:

```text
HECTON-8 demo/playtest is open
```

Body:

```text
The HECTON-8 [demo/playtest] is now available.

Current build focus:
- [Feature 1]
- [Feature 2]
- [Feature 3]

Current build does not include:
- co-op/multiplayer;
- final performance profile;
- final balance/content.

Steam:
[URL]

Feedback:
[URL]
```

## Newsletter Metrics

Track:

- subscribers;
- source;
- open rate;
- click rate;
- unsubscribe rate;
- Steam visits from email;
- wishlists from email where measurable;
- demo feedback submissions.

Ignore vanity subscriber count if clicks are weak.

## Current HECTON-8 Decision

Prepare forms/copy now. Do not push email signup aggressively until first screenshot or Steam page exists.
