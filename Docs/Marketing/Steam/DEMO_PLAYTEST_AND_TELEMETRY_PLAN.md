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

## Demo Purpose

The first public demo must answer:

- What is HECTON-8?
- What does the player do every minute?
- What is dangerous?
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
5. One readable threat/failure state.
6. One Seed Ship/anomaly signal.
7. Return/repair/upgrade loop.
8. End screen with wishlist/feedback CTA.

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
demo_complete
wishlist_cta_seen
feedback_cta_opened
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

- first route is playable start to finish;
- no crash in first 30 minutes on target hardware;
- save/load boundary decided;
- settings menu exists or limitations are stated;
- controls remap/accessibility boundary decided;
- known issues list exists;
- feedback form exists;
- Steam page CTA exists;
- no co-op implication;
- no unproved FPS claim.

## Playtest Gate

Steam Playtest can be used earlier, but still needs:

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
| No-coop boundary | Onboarding copy | Testers are told this is single-player-first and not a co-op test. |
| Hardware context | Screening form | Every low/mid-spec tester provides CPU/GPU/RAM/storage/settings. |

### Wave Result Decision

After each wave, choose exactly one:

| Decision | Use when | Next action |
|---|---|---|
| `EXPAND_WAVE` | Players reach first hook, describe pressure/machinery, and feedback is specific. | Add the next tester segment or Steam Playtest tranche. |
| `REVISE_BUILD` | Core loop works but friction repeats in UI, controls, resource route, darkness, or onboarding. | Fix the repeated tag before inviting more. |
| `STOP_PUBLIC_PATH` | Players cannot state what the game is, quit before hook, expect co-op, or performance blocks feedback. | Do not launch public demo; revise product/onboarding/assets. |

### Tag Escalation Rules

Stop expansion if any of these tags repeat across 3+ testers in a 25-person wave:

- `CLARITY_PLAYER_VERB`
- `CLONE_RISK`
- `DARKNESS_READABILITY`
- `SALVAGE_TEDIUM`
- `INVENTORY_FRICTION`
- `LOW_SPEC_PERF`
- `COOP_EXPECTATION`

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
Trailer/clip:
Known issues:
Feedback form:
Discord/forum link:
Press/creator batch:
Steam announcement:
UTM links:
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
- negative feedback is specific, not "what is this?";
- demo drives wishlist/follow;
- creator replies ask for longer build.

Failure signals:

- players quit before first hook;
- comments say "generic";
- resource loop called tedious repeatedly;
- UI/inventory dominates feedback;
- performance complaints overwhelm content feedback;
- players ask if co-op exists because marketing implied it.

## Current HECTON-8 Decision

Prepare Steam Playtest path and demo telemetry plan now. Do not expose a public demo until the first route proves the identity.
