# HECTON-8 Review Keys, Embargo, And Preview Access Protocol

Status: pre-key protocol / no keys ready
Owner lane: SHINOBU_81 / access control
Runtime impact: none

## Source Boundary

Primary current sources:

- Steam Keys: https://partner.steamgames.com/doc/features/keys
- Steam Curator Connect: https://partner.steamgames.com/doc/marketing/curators
- FTC Endorsement Guides: https://www.ftc.gov/business-guidance/resources/ftcs-endorsement-guides-what-people-are-asking
- YouTube Paid Promotions: https://support.google.com/youtube/answer/10588440?hl=en

Recheck before requesting keys or sending access. Platform limits and disclosure obligations are external rules.

## Access Types

| Access type | Use | HECTON-8 stance |
|---|---|---|
| Steam Curator Connect | Steam curator review copies | Preferred for curators. |
| Release State Override keys | Small press/influencer/beta access before release | Only after key request approval and strict log. |
| Steam Playtest | Larger public testing if needed | Consider for scale; separate from press keys. |
| Private build link | Emergency press preview only | Avoid unless secure and revocable. |
| Default release keys | Post-release review/press access | Later. |
| Dev comp keys | Internal developer access | Never distribute to press/customers. |

## Key Rules

- Never sell pre-release/access keys.
- Never promise keys before Valve approval.
- Never send keys to unverified contacts.
- Never use raw keys for Steam curators if Curator Connect works.
- Never send multiple keys because someone asks casually.
- Every key/access must have a reason, recipient, source, and status.
- Paid/sponsored coverage must disclose relationship.

## Release State Override Boundary

Steamworks documentation currently describes Release State Override keys as intended for small beta tests and press/influencer access, with a general total limit of 2,500. HECTON-8 must treat that number as an upper ceiling, not a target.

Recommended HECTON-8 allocation before launch:

| Purpose | Max before reassessment |
|---|---:|
| Press previews | 30 |
| Verified creators | 50 |
| Technical QA/playtest | 50 |
| Emergency replacements | 10 |
| Total first request | 140 or less |

If the team needs hundreds/thousands of players, evaluate Steam Playtest instead.

## Access Approval Flow

1. Recipient verified.
2. Fit score recorded.
3. Contact route verified from recipient-owned source.
4. Build route verified by QA.
5. Disclosure requirement identified.
6. Access type chosen.
7. Key/access logged.
8. Message sent.
9. Follow-up status tracked.
10. Unused/suspicious access reviewed.

## Verification Requirements

Press:

- publication exists;
- author or editor contact route is public/official;
- recent games coverage exists;
- outlet fits PC/indie/survival/horror;
- no key-resale pattern.

Creator:

- channel exists;
- recent relevant content;
- language and region known;
- public contact route;
- sponsorship policy if paid;
- no impersonator red flags.

Curator:

- use Curator Connect;
- recent review history;
- relevant tags;
- no external-key pressure.

## Embargo Use

Do not use embargoes for weak beats.

Use embargo only if:

- many recipients receive the same preview access;
- trailer/demo/release date is coordinated;
- assets are final enough;
- embargo time is concrete with timezone;
- recipient can realistically honor it;
- one person owns communication.

## Embargo Template

```text
Embargo:
You may publish coverage of HECTON-8 after [DATE] [TIME] [TIMEZONE].

Allowed before embargo:
- private questions by email;
- internal capture/prep;
- no public screenshots, clips, streams, posts, or reviews.

Allowed after embargo:
- gameplay footage from the provided build;
- screenshots from the provided asset pack;
- written/video impressions.

Please disclose any free review access according to your platform rules.
```

## No-Embargo Template

```text
There is no embargo for this HECTON-8 preview access. You may publish whenever ready.

Please note the build state honestly:
[build state]

Please disclose free access according to your platform rules.
```

## Preview Build Boundary Copy

Every preview access email must include:

```text
Current build includes:
- [Feature 1]
- [Feature 2]
- [Feature 3]

Current build does not include:
- co-op/multiplayer;
- [Feature not present];
- final performance profile;
- final balance/content.
```

## Key Log CSV Schema

```csv
key_id,batch_id,key_type,recipient_name,recipient_url,contact_route,purpose,region,language,build_version,sent_at,status,coverage_url,disclosure_required,notes
```

## 2026-05-19 Preview Access Batch V0

Status: protocol-only / no keys / no access authorized.

Use this as the first access batch design after a stable preview/demo build exists. Do not request or send keys from this table.

| Batch | Max size | Access type | Recipients | Required gates | Stop condition |
|---|---:|---|---|---|---|
| ACC-001 | 10 | Private preview or Steam Playtest invite | Wave A verified creators from CRM | Playtest decision gate passes, contact routes verified, build known issues ready. | Any recipient route cannot be verified or build has first-route blocker. |
| ACC-002 | 10 | Release State Override or approved preview access | Press rows marked ready after presskit | Press kit publish gate passes and route rechecked same day. | Any outlet asks for extra raw keys or route mismatch appears. |
| ACC-003 | 8 | Steam Curator Connect | Curator tracker first-copy candidates | Public Steam page/build exists; Curator Connect used, not raw keys. | Curator asks for external keys or page is stale/formulaic. |
| ACC-004 | 10 | Technical QA/playtest | Low/mid-spec testers selected by score | Hardware specs collected; feedback tags ready. | Performance blocks all content feedback. |

### Access Message Must Include

```text
Access type:
Build version:
Allowed capture:
Known issues:
Current build includes:
Current build does not include:
Disclosure requirement:
Feedback route:
Support contact:
```

### Access Stop Rules

Stop sending access if:

- 2 recipients report a build blocker in the first route;
- key/access route leaks outside verified contacts;
- recipient asks for false talking points;
- disclosure language is rejected;
- feedback is dominated by co-op expectation caused by our copy;
- Steam/Curator/YouTube/FTC rules have not been rechecked that week.

## Scam Red Flags

- "Send 5 keys for our review team" with no staff proof;
- Gmail-only contact claiming large outlet;
- Discord DM from curator;
- curator asks for external key after Curator Connect;
- no recent content;
- outlet/channel has unrelated content only;
- recipient refuses disclosure language;
- urgent pressure for keys before verification.

## Polite Denial

```text
Thanks for reaching out. We are only distributing HECTON-8 preview access through verified press/creator routes and Steam Curator Connect where applicable. I cannot send raw keys through this channel.
```

## Current HECTON-8 Decision

No keys yet. Prepare log and policy only. Actual key/access distribution waits for Steam page, playable build, verified recipient list, and QA route.
