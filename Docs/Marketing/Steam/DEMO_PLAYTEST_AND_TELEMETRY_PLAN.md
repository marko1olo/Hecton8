# HECTON-8 Demo, Steam Playtest, And Telemetry Plan

Status: pre-demo operating plan
Owner lane: SHINOBU_81 / demo funnel
Runtime impact: none

## Source Boundary

Current primary sources checked on 2026-05-19:

- Steam Playtest: https://partner.steamgames.com/doc//features/playtest
- Steam Next Fest: https://partner.steamgames.com/doc/marketing/upcoming_events/nextfest?language=english
- Steam Events and Announcements Tools: https://partner.steamgames.com/doc/marketing/event_tools?l=english&language=english
- Steam User Reviews: https://partner.steamgames.com/doc/store/reviews?l=english&language=english

Recheck before public demo, Playtest, or Next Fest registration.

## Demo vs Playtest

| Route | Use | Benefits | Risks |
|---|---|---|---|
| Public Steam Demo | Marketing, Next Fest, wishlist conversion | Players can try immediately; strong CTA. | Weak demo creates public damage. |
| Steam Playtest | Controlled testing, feedback, scaling before demo | Separate child app; signup on main page; lower risk to main reviews/wishlists. | Less public marketing punch; still needs management. |
| Private preview build | Press/creator review | Controlled access. | Key/access leakage; support overhead. |

Default recommendation: use Steam Playtest or private preview for rough feedback before exposing a public demo.

## Public CTA vs Private Access Boundary V0

Do not mix public traffic links with private access routes.

## Public Demo / Playtest Permission Gate

Current machine gate: `demo_public_access_permission_gate = HOLD_NO_PUBLIC_DEMO_ACCESS`.

Future allow value: `ALLOW_PUBLIC_DEMO_ACCESS_VERIFIED`.

This gate applies to public Steam demo release, public demo button, Next Fest demo availability, public Steam Playtest signup/tranche, public "demo is live" claims, and any public demo/playtest feedback route.

Do not infer public demo access permission from a build launching, Steam page publication, CTA approval, private access approval, a known-issues draft, a feedback form, a public announcement draft, or "first route playable" prose alone.

`ALLOW_PUBLIC_DEMO_ACCESS_VERIFIED` requires:

- exact app/demo/playtest surface, build ID, owner, rollback/disable owner, and current official Steamworks rule recheck;
- first route playable start to finish, no first-30-minute crash on target hardware, save/load boundary, controls/settings/accessibility boundary, and known-issues copy;
- Steam page `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED` and destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` for every public demo/playtest link;
- `steam_support_permission_gate = ALLOW_STEAM_SUPPORT_ROUTE_VERIFIED`, named support/bug triage owners, and public feedback route with `route_class` plus `consent_provenance`;
- `steam_announcement_permission_gate`, `public_post_permission_gate`, `press_release_permission_gate`, `submission_permission_gate`, and `spend_permission_gate` still gate announcement, post, release, event, and paid traffic separately;
- AB-009/KPI decision-read fields before public copy claims gameplay/pressure/route-risk/threat/salvage/base-failure proof;
- no unsupported multiplayer-scope, performance, date, feature, or competitor-war claim.

| Route | Link class | Allowed use | Kill if |
|---|---|---|---|
| Public Steam demo | Public CTA | Steam page, Next Fest, public posts, press/showcase traffic after `demo_public_access_permission_gate` and `Analytics/MEASUREMENT_AND_UTM_PLAN.md` CTA activation. | The demo URL is hidden/draft, wrong app, missing known issues, linked before CTA activation, or public demo access is not explicitly allowed. |
| Steam Playtest | Controlled signup/access | Screened tester waves and product feedback before public demo; public signup/tranche still requires `demo_public_access_permission_gate`. | It is advertised as a public demo, opened from build existence, or used to inflate wishlist/signup hype. |
| Private preview build | Private access | Verified creator/press preview through `Press/REVIEW_KEYS_EMBARGO_AND_PREVIEW_ACCESS_PROTOCOL.md`. | The link is posted publicly, shared through social bio, or reused as a showcase CTA. |
| Feedback form | Support/research route | Demo/playtest feedback after access is legitimate. | It collects emails or promises access without owned-audience consent/custody. |

Public demo/Playtest access requires `demo_public_access_permission_gate = ALLOW_PUBLIC_DEMO_ACCESS_VERIFIED`. Public CTA links require `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`. Private access links require `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`, build known-issues copy, revocation/stop rules, and access-log fields: `verified_contact_route`, `access_route_class`, `reply_status_after_send`, `reply_consent_provenance`, plus `agency_decision_field_source` where proof claims are used. One link cannot serve both purposes.

If public demo, Steam Playtest, or private preview copy claims gameplay, pressure, route risk, threat, salvage, base failure, or first-public agency proof, the copy also needs AB-009/KPI support: `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`. Demo telemetry may create later proof, but it does not justify the access pitch before the field exists.

## Demo Purpose

The first public demo must answer:

- What is HECTON-8?
- What does the player do every minute?
- What is dangerous?
- What decision does the player make under pressure?
- Why is this not generic underwater survival?
- Does the build respect player time?
- Does it run acceptably on stated hardware?

If the demo cannot answer these, do not release it.

## First Demo Scope

Recommended vertical slice:

1. Wake/entry context under 60 seconds.
2. First pressure/machinery problem.
3. Short salvage route.
4. Base or module interaction.
5. One readable threat/failure state with a player choice: avoid, reroute, seal, scan, retreat, or continue.
6. One Seed Ship/anomaly signal after the pressure-choice beat exists.
7. Return/repair/upgrade loop.
8. End screen with public CTA only after the exact demo/Steam route gate and destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`, or private feedback route for controlled playtests.

Target length:

- 20-35 minutes for average player;
- 45-60 minutes for explorers;
- no 2-hour grind;
- no dead travel longer than 60 seconds without tension/choice.

## Telemetry Without Runtime Scope Creep

This doc is marketing/product telemetry planning, not an implementation mandate. Engineering decides implementation later.

Required questions:

| Question | Metric |
|---|---|
| Do players reach the first hook? | time to first meaningful action |
| Do players understand survival pressure? | first failure/correction point |
| Where do they quit? | exit segment |
| What confuses them? | feedback tag + segment |
| Does resource routing feel tedious? | repeated route count before progression |
| Does base interaction work? | completion/failure on first base task |
| Do players understand agency under pressure? | first route choice described without prompting |
| Does Seed Ship hook create curiosity? | end-survey interest answer |
| Does demo drive Steam action? | wishlist/follow after demo session |

## Lightweight Event Taxonomy

Use stable names if telemetry is implemented.

```text
demo_start
first_control
first_pressure_warning
first_tool_use
first_salvage_pickup
first_inventory_full
first_base_interaction
first_repair_success
first_route_return
first_death_or_failure
seed_ship_signal_seen
first_pressure_choice
demo_complete
public_cta_seen
feedback_route_opened
```

## Feedback Survey

Use 6 questions max.

1. What made you want to keep playing?
2. What made you want to stop?
3. What was unclear?
4. Did the resource loop feel tense or tedious?
5. Which moment felt most HECTON-8?
6. Would you wishlist or follow the game after this demo? Why?

Do not ask 30 questions. Players quit forms.

## Demo QA Gate

Public demo cannot ship until:

- `demo_public_access_permission_gate = ALLOW_PUBLIC_DEMO_ACCESS_VERIFIED` for the exact app/demo/playtest surface;
- first route is playable start to finish;
- no crash in first 30 minutes on target hardware;
- save/load boundary decided;
- settings menu exists or limitations are stated;
- controls remap/accessibility boundary decided;
- known issues list exists;
- feedback form exists;
- Steam page `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED` and `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` exist for public demo, or controlled Playtest/preview has `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED` plus `verified_contact_route`, `access_route_class`, `reply_status_after_send`, and `reply_consent_provenance`;
- public demo/feedback routes have `route_class` plus `consent_provenance`; private access routes have the exact access-log field set above;
- AB-009/KPI field exists before marketing copy claims gameplay/pressure/route-risk agency proof;
- no unsupported multiplayer-scope implication;
- no unproved FPS claim.

## Playtest Gate

Steam Playtest can be used earlier, but public signup/tranches still need `demo_public_access_permission_gate = ALLOW_PUBLIC_DEMO_ACCESS_VERIFIED` and:

- onboarding copy;
- known issues;
- feedback route;
- crash reporting/manual bug form;
- tester invite logic;
- NDA decision if private;
- timebox.

## 2026-05-19 Playtest Decision Gate V0

Status: blocked until first route is playable and screening score exists.

Do not open Steam Playtest or private preview just because the build launches. The first wave must answer whether the core loop is understandable.

### Required Before First External Wave

| Gate | Required source | Pass condition |
|---|---|---|
| Screening | `Audience/PLAYTESTER_RECRUITMENT_AND_SCREENING_PLAN.md` | First 25 testers selected by score and segment quota. |
| Route | Current playable build | One start-to-return route completes without blocker. |
| Known issues | Support/QA note | Known issues written in plain language before access. |
| Feedback tags | Screening plan tag list | Feedback form or tracker accepts the canonical tags. |
| Agency proof field | AB-009/KPI or wave feedback log | `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision` exists before proof is reported or reused in access copy. |
| Route/provenance | access/feedback log | `verified_contact_route`, `access_route_class`, `reply_status_after_send`, and `reply_consent_provenance` are recorded before replies count outside the original test route; `agency_decision_field_source` is recorded if proof claims are used. |
| Multiplayer-scope boundary | Onboarding copy | Testers are told this is a single-player-first test of the current build. |
| Hardware context | Screening form | Every low/mid-spec tester provides CPU/GPU/RAM/storage/settings. |

### Wave Result Decision

After each wave, choose exactly one:

| Decision | Use when | Next action |
|---|---|---|
| `EXPAND_WAVE` | Players reach first hook, describe pressure/machinery, name a decision under pressure, and `verified_contact_route`, `access_route_class`, `reply_status_after_send`, `reply_consent_provenance`, and any needed `agency_decision_field_source` are usable. | Add the next tester segment or Steam Playtest tranche. |
| `REVISE_BUILD` | Core loop works but friction repeats in UI, controls, resource route, darkness, or onboarding. | Fix the repeated tag before inviting more. |
| `STOP_PUBLIC_PATH` | Players cannot state what the game is, quit before hook, expect unsupported multiplayer scope, or performance blocks feedback. | Do not launch public demo; revise product/onboarding/assets. |

### Tag Escalation Rules

Stop expansion if any of these tags repeat across 3+ testers in a 25-person wave:

- `CLARITY_PLAYER_VERB`
- `CLONE_RISK`
- `DARKNESS_READABILITY`
- `SALVAGE_TEDIUM`
- `INVENTORY_FRICTION`
- `LOW_SPEC_PERF`
- `MULTIPLAYER_SCOPE_EXPECTATION`

## Demo Patch Policy

During public demo:

- patch crashes fast;
- patch blockers fast;
- do not rebalance daily unless clearly broken;
- write short patch notes;
- never hide known issues;
- do not promise dates in comments.

## Demo Launch Day Checklist

```text
Steam page:
Demo build:
demo_public_access_permission_gate if any public demo or public Steam Playtest access is used:
Trailer/clip:
Known issues:
Feedback form:
Discord/forum link:
steam_support_permission_gate if Steam forum/support is used:
Press/creator batch:
Steam announcement:
steam_announcement_permission_gate:
steam_page_publish_permission_gate if public Steam/demo page is used:
UTM links:
CTA route_class if public:
verified_contact_route if private:
access_route_class if private:
reply_status_after_send:
reply_consent_provenance:
agency_decision_field_source if proof claims are used:
Support owner:
Bug triage owner:
Go/no-go:
```

## Demo Success Thresholds

Do not judge by raw downloads only.

Useful signals:

- >60% reach first core action;
- >40% reach first base/repair loop;
- players describe pressure/machinery without prompting;
- players can name one decision they made under pressure;
- decision proof is stored in `agency_decision_read` or `cold_read_agency_decision` before reuse;
- negative feedback is specific, not "what is this?";
- demo drives wishlist/follow;
- creator replies ask for longer build.

Failure signals:

- players quit before first hook;
- comments say "generic";
- resource loop called tedious repeatedly;
- UI/inventory dominates feedback;
- performance complaints overwhelm content feedback;
- players ask if multiplayer exists because marketing implied it.

## Current HECTON-8 Decision

Prepare Steam Playtest path and demo telemetry plan now. Do not expose a public demo until the first route proves the identity.
