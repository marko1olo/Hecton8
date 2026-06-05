# One-Page Site And Presskit Plan

Status: future website/presskit shell
 Public stance: single-player-first scope / proof-first public copy
Runtime impact: none

## Authority Boundary

Static website and presskit plan only. Public voice routes through root `textes.md`.
Any quality, release, platform, Steam, wishlist, demo, performance, press, review-key, embargo, showcase, curator, asset-QA, schedule, SEO, pricing, discount, publication, website, or presskit claim requires `quality.md`, `release.md`, `platform.md`, current proof artifacts, and the matching local permission gate.
Site sections, factsheet rows, presskit checklists, schedules, and strategy rows do not approve Steam page, wishlist, pricing, discount, review-key, presskit, showcase, publication, platform, or release claims.

## Objective

Create a small, fast, boringly useful site when Steam page and assets exist. The site should serve press, creators, and players who need one link with official information.

Do not make a marketing vanity page before Steam is ready.

## Site Sections

| Section | Content |
|---|---|
| Hero | One real gameplay image/video with readable player decision backed by AB-009/KPI decision-read fields, title, one-line pitch, Steam CTA. |
| What It Is | Single-player-first underwater survival: beautiful alien surface/shallows, pressure, machinery, salvage, and black-water depth risk. |
| Screenshots | 6-10 real images, downloadable. |
| Trailer/Clips | trailer and short clips. |
| Factsheet | developer, platform, genre, status, contact, links. |
| Press Kit | logos, capsules, screenshots, trailer, fact sheet. |
| Creator Info | private access gate policy, disclosure language, contact route. |
| FAQ | multiplayer-scope boundary, performance claim boundary, demo status. |

## One-Line Pitch

Submerge is a single-player-first underwater survival game where a beautiful alien ocean, pressure, machinery, salvage, and black-water depth risk decide whether you make it back alive.

Website visual lock: spoiler-safe public pages may sell beauty when screenshots show surface, Aegir, coastline, ocean skin, or photic shallows. Do not make the whole site read as permanently dark water. Darkness belongs to depth, caves, interiors, storms, and temporary eclipse windows.

Naming caveat: `Submerge` is the owner-selected public title candidate as of 2026-05-31. Keep `HOLD_NAMING_CONFLICT_REVIEW` until the exact-title Steam conflict and domain/social route are resolved.

## Factsheet Fields

| Field | Value |
|---|---|
| Title | Submerge - HOLD_NAMING_CONFLICT_REVIEW |
| Internal codename | HECTON-8 |
| Genre | Single-player underwater survival / exploration / base systems |
| Platforms | HOLD_PLATFORM_LIST_UNVERIFIED - publish only after owner/platform source exists. |
| Release | HOLD_NO_RELEASE_WINDOW - do not publish a date/window from planning prose. |
| Demo | HOLD_NO_PUBLIC_DEMO_ACCESS - public demo/Playtest only after `demo_public_access_permission_gate = ALLOW_PUBLIC_DEMO_ACCESS_VERIFIED`. |
| Developer | HOLD_LEGAL_NAME_UNVERIFIED - publish only after owner-approved legal/public credit. |
| Contact | HOLD_NO_PROJECT_INBOX_CUSTODY - official project email only after `official_inbox_custody_gate = ALLOW_OFFICIAL_INBOX_USE_VERIFIED`. |
| Steam | HOLD_NO_STEAM_PAGE_PUBLICATION - link only after `steam_page_publish_permission_gate` and destination-specific `public_cta_permission_gate`. |
| Press kit | HOLD_NO_PRESS_RELEASE_PUBLICATION - public presskit link only after `press_release_permission_gate` and destination-specific `public_cta_permission_gate`. |
| Public stance | Single-player-first scope, proof-first public copy |

## Presskit Folder Structure

```text
PressKit/
  Factsheet_Submerge.md
  Logos/
    Submerge_Logo_Light.png
    Submerge_Logo_Dark.png
    Submerge_Logo_Transparent.png
  Capsules/
    Steam_HeaderCapsule.png
    Steam_SmallCapsule.png
    Steam_MainCapsule.png
    Steam_LibraryCapsule.png
  Screenshots/
    Submerge_Screenshot_01_PressureMachinery.png
    Submerge_Screenshot_02_SalvageRoute.png
    Submerge_Screenshot_03_BasePressureVessel.png
  Video/
    Submerge_Trailer_1080p.mp4
    Submerge_Short_20s_PressureWarning.mp4
  Creator/
    Creator_Key_Disclosure.md
    Coverage_Guidelines.md
  README.md
```

## Creator Disclosure Text

If you received a key/build from us, use plain disclosure such as:

"Demo/key provided by the developer."

Do not ask creators to hide sponsorship, payment, keys, or relationship.

## Website Copy Blocks

### What Is Submerge?

Submerge is a single-player-first underwater survival game about pressure, machinery, salvage, and black-water exploration. You survive by operating and repairing systems that keep the ocean outside.

### What Makes It Different?

Submerge is aimed at industrial deep-sea noir: corrosion, floodlights, instruments, worn machinery, hostile visibility, and a Seed Ship anomaly that makes the environment stop feeling neutral.

### Scope

HECTON-8 public scope is single-player-first. Performance language waits for measured build proof.

## Launch Gate

Do not publish the site until:

- Official CTA Link Activation Gate V0 passes for any Steam link;
- official contact exists;
- `press_release_permission_gate = ALLOW_PRESS_RELEASE_PUBLISH_VERIFIED` for any public presskit announcement, press release block, media one-pager, or "presskit is live" copy;
- at least 6 real screenshots exist;
- at least one screenshot or clip proves a readable player decision under threat, leak, route cost, sonar pressure, or salvage failure, with non-pending `viewer_named_decision`, valid `capture_verdict`, and `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision` recorded for first-page proof;
- presskit assets exist;
- no placeholder claims remain;
- no unsupported multiplayer-scope hints remain.

## 2026-05-19 No-Link Holding State V0

If accounts or a domain are reserved before Steam is ready, do not publish a fake launch page. Use a minimal holding state only when there is a real official contact route.

2026-05-31 owner update: do not buy domains now. Keep domain work as search/name-risk notes only. Current state: `HOLD_NO_DOMAIN_PURCHASE`.

## 2026-05-19 Official Project Inbox Gate V0

No social account, presskit, curator batch, key email, paid creator term, or public contact field should use a personal inbox, throwaway inbox, or agent-owned inbox.

Do not treat an already logged-in browser profile, cookies, remembered passwords, or chat permission as inbox/account custody. Custody starts only when the non-secret fields below are recorded and the secrets live in the owner password manager.

Machine gate:

- current value: `official_inbox_custody_gate = HOLD_NO_PROJECT_INBOX_CUSTODY`;
- future allow value: `ALLOW_OFFICIAL_INBOX_USE_VERIFIED`;
- `ALLOW_OFFICIAL_INBOX_USE_VERIFIED` requires owner-approved durable address, vault item, owner-controlled recovery, 2FA enabled, backup codes stored in the vault, required labels/folders, approved reply identity, and public-contact approval when the inbox is published.

Do not infer inbox permission from an address field alone.

## 2026-05-31 Pre-Screenshot Owner Setup Packet V1

This packet can be prepared now. It is non-secret. Passwords, 2FA seeds, backup codes, cookies, session tokens, and recovery codes must not be stored in docs or chat.

Owner note, 2026-05-31: the owner authorized two personal Gmail accounts in chat for account work. Treat them as owner-supplied candidate login/recovery inboxes only. Do not store the raw addresses in docs. Do not publish a personal Gmail as press/support contact if a domain-based project inbox can be created. If a personal Gmail is used temporarily, keep it as accounts/recovery custody, not public identity.

### Owner Tasks

1. Pick the project identity route: domain-based email or durable provider inbox.
2. Create the password manager vault structure before creating accounts.
3. Store secrets only in the vault.
4. Fill the non-secret custody fields below after the owner verifies them.
5. Do not publish the inbox until public-contact approval is explicit.

### Recommended Vault Item Names

| Vault item | Purpose |
|---|---|
| `HECTON-8 / Domain Registrar` | Domain, DNS, renewal, recovery. |
| `HECTON-8 / Official Inbox` | Main project email and recovery. |
| `HECTON-8 / Steamworks` | Steamworks credentials and partner records after creation. |
| `HECTON-8 / Social / YouTube` | YouTube brand account after creation. |
| `HECTON-8 / Social / X` | X/Twitter account after creation. |
| `HECTON-8 / Social / Bluesky` | Bluesky account after creation. |
| `HECTON-8 / Audience Provider` | Newsletter/form provider after selection. |
| `HECTON-8 / Support Local RU` | Boosty/DonationAlerts/VK Donut only if approved. |
| `HECTON-8 / Platform Tax And Payout` | Non-public tax, bank, and payout records. |

### Non-Secret Custody Fields

| Field | Current state | Pass state |
|---|---|---|
| Project identity route | UNDECIDED | DOMAIN_EMAIL or DURABLE_PROVIDER_EMAIL |
| Domain candidate | UNRECORDED_NOT_PUBLISHABLE | Owner-approved domain or HOLD_NO_DOMAIN |
| Official inbox address | UNRECORDED_NOT_PUBLISHABLE | Owner-approved durable address |
| Provider/domain | UNRECORDED_NOT_PUBLISHABLE | Owner-controlled provider/domain |
| Vault item name | UNRECORDED_NOT_PUBLISHABLE | Exact vault item name, no secret value |
| Recovery owner | NO | Owner-controlled recovery recorded in vault |
| 2FA method chosen | NO | App/security-key/SMS decision recorded in vault |
| Backup-code custody | NO | Stored in vault, not docs |
| Labels/folders | NO | `Accounts`, `Creators`, `Press`, `Keys`, `Support`, `Legal`, `Receipts` |
| Public contact approval | NO | Explicit owner approval after proof gates |

### Browser Work Boundary

Agent-opened browser tabs are not custody proof. A tab can start onboarding, but `official_inbox_custody_gate` stays held until the owner has completed login, recovery, 2FA, backup-code storage, labels, vault record, and public-contact approval.

Do not ask for passwords or 2FA in chat. Do not record cookies, session tokens, app passwords, backup codes, or recovery codes.

### Minimum No-Link Holding Page Draft

```text
Submerge
Internal codename: HECTON-8

Single-player deep-sea survival about pressure, machinery, salvage, and black-water exploration.

Public screenshots, Steam page, and demo details will be posted when they are ready.

Contact: HOLD_OFFICIAL_INBOX_PUBLICATION
```

Replace the contact line only after `official_inbox_custody_gate = ALLOW_OFFICIAL_INBOX_USE_VERIFIED` and public-contact approval are both true.

Minimum owner-controlled inbox requirements:

| Requirement | Pass state |
|---|---|
| Address | Uses project/domain identity or an owner-controlled durable provider address approved for HECTON-8. |
| Password custody | Stored in owner password manager, not in docs or chat. |
| Recovery | Recovery email/phone is owner-controlled and recorded in the vault. |
| 2FA | Enabled before the inbox is used for account registration or creator/press contact. |
| Backup codes | Stored in the vault before any public account depends on the inbox. |
| Labels/folders | `Accounts`, `Creators`, `Press`, `Keys`, `Support`, `Legal`, `Receipts`. |
| Reply identity | Display name `HECTON-8` or approved owner/team name; no fake studio geography. |
| Signature | Includes single-player-first/multiplayer-scope boundary only when relevant; no Steam/demo links until Official CTA Link Activation Gate V0 for public links or recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED` plus exact access-log fields for private routes. |

Recommended aliases if the provider/domain supports them:

| Alias | Use |
|---|---|
| `press@...` | Presskit, press questions, event/showcase contacts. |
| `creators@...` | Creator access and key/demo requests. |
| `support@...` | Demo/EA support only after build proof, owner-controlled support inbox/form custody, `steam_support_permission_gate = ALLOW_STEAM_SUPPORT_ROUTE_VERIFIED` where Steam support is involved, and destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` if the route is linked publicly. |
| `accounts@...` | Platform registration/recovery only; do not publish publicly. |

If aliases are not available, use one official inbox with labels. Do not create separate untracked mailboxes.

### Inbox Custody Record V0

This is a non-secret checklist. Store secrets only in the owner password manager.

| Field | State |
|---|---|
| `official_inbox_custody_gate` | HOLD_NO_PROJECT_INBOX_CUSTODY |
| Official inbox address | UNRECORDED_NOT_PUBLISHABLE |
| Provider/domain | UNRECORDED_NOT_PUBLISHABLE |
| Vault item name | UNRECORDED_NOT_PUBLISHABLE |
| Recovery owner verified | NO |
| 2FA enabled | NO |
| Backup codes stored | NO |
| `Accounts` label exists | NO |
| `Creators` label exists | NO |
| `Press` label exists | NO |
| `Keys` label exists | NO |
| `Support` label exists | NO |
| `Legal` label exists | NO |
| Public contact approved | NO |

Do not publish the inbox or use it for social registration until recovery owner, 2FA, backup-code custody, and labels are complete.

### Allowed Holding Page

```text
Submerge
Single-player-first underwater survival about pressure, machinery, salvage, and black-water exploration.

Public screenshots, Steam page, and demo details will be posted when they are ready.
Contact: [official project email]
```

### Holding Page Must Not Include

- wishlist CTA before Official CTA Link Activation Gate V0;
- mailing list unless the owned-audience gate has a concrete promise;
- fake screenshots, target renders, or AI-looking concept art;
- roadmap dates;
- unsupported multiplayer-scope hints;
- performance claims;
- "Subnautica killer" or competitor comparison;
- Discord invite before moderation and purpose are ready.

## Presskit Minimum Viable Packet

Machine gate for public use:

- current value: `press_release_permission_gate = HOLD_NO_PRESS_RELEASE_PUBLICATION`;
- future allow value: `ALLOW_PRESS_RELEASE_PUBLISH_VERIFIED`;
- folder completeness, a contact address, or a public CTA packet cannot publish or announce the presskit by itself.

Do not send a presskit link until the folder contains at least:

| File | Minimum requirement |
|---|---|
| Factsheet | Title, genre, developer, official contact, platform/status, single-player-first scope statement. |
| Screenshots | 6 approved real in-game captures with asset metadata and QA pass; lead set includes one readable player decision recorded in AB-009/KPI fields. |
| Clips/trailer | At least one real gameplay clip or trailer beat; no target render; one beat shows a player choice under pressure. |
| Logo/capsule | Current approved logo/capsule draft with status label if not final. |
| Creator/disclosure | Private access/key policy, recipient/batch gate requirement, and disclosure language. |
| Known limits | Demo/status/performance boundaries stated plainly. |
| README | What can be used, what is placeholder, and who to contact. |

### Presskit Send Kill Conditions

- any file name says `final` while the asset is still draft;
- screenshots cannot be traced to build ID or asset metadata;
- screenshots/clips are mood-only, do not prove a player decision, or lack the required AB-009/KPI decision-read field for first-page proof;
- factsheet says or implies unsupported multiplayer scope;
- performance is described without a proof link;
- contact email is personal or throwaway;
- the kit cannot be downloaded without account friction.
