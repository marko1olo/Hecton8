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
- Do not promise unsupported multiplayer modes, release dates, or performance.
- Every signup form must say what people are signing up for.
- Every email must have an unsubscribe path.
- Do not publish signup forms, send list emails, or import contacts unless `owned_audience_permission_gate = ALLOW_OWNED_AUDIENCE_VERIFIED` for the exact mode.

## Signup Offers

Use only honest offers:

| Offer | When to use | Copy |
|---|---|---|
| Demo alert | Before demo | "Get one email when the HECTON-8 demo/playtest opens." |
| Playtest waitlist | Before Steam Playtest/private playtest | "Join the playtest waitlist for single-player deep-sea survival feedback." |
| Devlog digest | After first screenshot packet passes QA and AB-009/KPI agency proof | "Occasional devlog updates on pressure, salvage, machinery, player decisions, and the Seed Ship." |
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

Machine gate: `owned_audience_permission_gate = HOLD_NO_OWNED_AUDIENCE`. The only future allow value is `ALLOW_OWNED_AUDIENCE_VERIFIED`, and it is mode-specific: approving `DEMO_ALERT` does not approve `PLAYTEST_WAITLIST`, `DEVLOG_DIGEST`, `PRESS_CREATOR_CONTACT`, regional lists, or support lists.

Do not publish a signup form, send list email, import contacts, or count signup signal until the official inbox/list provider is owner-controlled, unsubscribe/delete works, `consent_provenance` is recorded, and every linked Steam/demo/playtest/feedback URL passes `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` or the relevant private access route passes `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`.

### Form Provider Custody Gate V0

Do not publish a form from a personal Google account, agent account, disposable form provider, or unrecorded workspace.

| Requirement | Pass state |
|---|---|
| Form owner | Owner-controlled project account or approved list provider workspace. |
| Data purpose | Signup mode is explicit before submit. |
| Consent | Checkbox text matches the segment and send cadence. |
| Unsubscribe/delete | User can unsubscribe or request removal through the official route. |
| Export custody | Export location is owner-controlled and not mixed with creator CRM or press tracker by default. |
| CTA route | Public form URL passes CTA activation if used outside direct/invite-only recruitment. |

If any field is missing, keep the copy as a draft and do not publish the form.

`ALLOW_OWNED_AUDIENCE_VERIFIED` additionally requires:

- `official_inbox_custody_gate = ALLOW_OFFICIAL_INBOX_USE_VERIFIED`;
- owner-controlled provider workspace or project account;
- signup mode and data purpose visible before submit;
- consent checkbox text matching the mode and cadence;
- unsubscribe/delete route tested;
- export custody owner and storage path recorded;
- creator CRM, press tracker, curator tracker, playtest screening, support, and newsletter contacts kept separate by default;
- `route_class` and `consent_provenance` planned before any signup count is reported;
- no bought, scraped, copied, or imported list.

### Signup Modes

| Mode | When allowed | Fields | Promise | Stop condition |
|---|---|---|---|---|
| `DEMO_ALERT` | Steam page or demo/playtest is close enough to define honestly. | Email, consent, preferred language. | One email when demo/playtest opens. | Stop if demo scope/date is unknown. |
| `PLAYTEST_WAITLIST` | First route is playable internally and screening score is active. | Email, consent, language, region, hardware opt-in, segment interest. | Possible invite, not guaranteed access. | Stop if build cannot support external testers. |
| `DEVLOG_DIGEST` | First screenshot pack has passed QA and includes one agency/decision proof asset with non-pending `viewer_named_decision`, valid `capture_verdict`, and AB-009/KPI decision-read fields. | Email, consent, preferred language. | Occasional major updates only. | Stop if updates would be filler or mood-only. |
| `PRESS_CREATOR_CONTACT` | Presskit exists. | Work email/contact, outlet/channel, consent. | Press/creator updates only. | Stop if presskit/contact policy is not ready. |

### Signup Copy Blocks

#### Demo Alert

```text
Get one email when the HECTON-8 demo or playtest opens.

HECTON-8 is single-player deep-sea survival about pressure, salvage, machinery, and black water. Proof-first updates only; no weekly filler.
```

#### Playtest Waitlist

```text
Apply for future HECTON-8 playtest waves.

This is for feedback on a single-player survival build: clarity, pressure systems, salvage, base machinery, controls, and performance context. Access is not guaranteed.
```

#### Devlog Digest

```text
Occasional HECTON-8 development updates when there is something real to show: screenshots with a readable player decision, Steam page, demo/playtest, or major systems notes.
```

### List Hygiene

- Do not import creator/press CRM rows into the audience list.
- Do not add anyone without explicit consent.
- Segment by signup mode at collection time.
- Keep playtest screening, creator CRM, press contacts, and newsletter subscribers in separate consent buckets.
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
- only if asset/update is meaningful;
- no devlog push from mood/anomaly screenshots unless agency/decision proof and AB-009/KPI decision-read fields already exist.

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

This list is for major updates only: first screenshots with a readable player decision, Steam page, demo/playtest access, and launch news. No scope claims without proof, no fake performance claims, no weekly filler.

Steam: [gated Steam URL after `steam_page_publish_permission_gate` and `public_cta_permission_gate` pass]
```

## Demo Alert Email

Subject:

```text
HOLD_DEMO_OPEN_SUBJECT - use demo/playtest-open subject only after `demo_public_access_permission_gate`, owned-audience permission, public CTA custody, and current build/known-issues source pass.
```

Body:

```text
HOLD_DEMO_AVAILABILITY_COPY - announce demo/playtest availability only after `demo_public_access_permission_gate = ALLOW_PUBLIC_DEMO_ACCESS_VERIFIED`, owned-audience permission, public CTA custody, and current build/known-issues source pass.

Current build focus:
- HOLD_FEATURE_1 - exact approved demo/playtest feature from current build scope.
- HOLD_FEATURE_2 - exact approved demo/playtest feature from current build scope.
- HOLD_FEATURE_3 - exact approved demo/playtest feature from current build scope.

Current build does not include:
- unsupported multiplayer modes;
- final performance profile;
- final balance/content.

Steam:
[gated Steam URL after `steam_page_publish_permission_gate` and `public_cta_permission_gate` pass]

Feedback:
[gated feedback URL after `owned_audience_permission_gate` or `steam_support_permission_gate`, plus `public_cta_permission_gate`, pass]
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

Prepare forms/copy now. Do not push email signup until `owned_audience_permission_gate = ALLOW_OWNED_AUDIENCE_VERIFIED`, the first screenshot packet includes agency/decision proof with AB-009/KPI decision-read fields or the Steam page CTA is activated, and the owned-audience URL passes `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`.
