# Scene Flow Authority Drift - 2026-06-05

Status: STATIC CONFLICT / OWNER DECISION REQUIRED

Evidence class: `STATIC_SOURCE`, `STATIC_DOC`, `STATIC_FILESYSTEM`.

## Conflict

Root `AGENTS.md` currently states normative scene flow:

`00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`

Current architecture docs and BuildSettings include:

`00_BOOTSTRAP -> 01_MAIN_MENU -> 01_ORBIT -> 02_HECTON_WORLD`

This is not a harmless doc mismatch. The first-20 proof route, New Game proof, save/load proof, and visual packet route can target different flows unless owner/integrator resolves it.

## Static Evidence

- `AGENTS.md:66` says `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`.
- `ProjectSettings/EditorBuildSettings.asset` enables `00_BOOTSTRAP`, `01_MAIN_MENU`, `01_ORBIT`, and `02_HECTON_WORLD`.
- `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md` requires New Game through `01_ORBIT`.
- `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md` records `01_ORBIT` in active scene spine and explicitly marks AGENTS drift unresolved.
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` declares constants for `00_BOOTSTRAP`, `01_MAIN_MENU`, `01_ORBIT`, and `02_HECTON_WORLD`.
- `Assets/_Project/Scripts/Core/GameStartContext.cs` still documents New Game and Load Game as direct `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`.

## Risk

- A Unity owner may prove direct world load while product docs expect orbit handoff.
- A prologue owner may wire orbit while root AGENTS still rejects it as main handoff.
- ProofGate h8_1475 route predicates may not match actual New Game route.
- Save/load restoration may be validated against the wrong first entry scene.

## Required Decision

Owner/integrator must choose one:

- promote `01_ORBIT` as product New Game handoff and update root authority;
- demote `01_ORBIT` to optional/debug/prologue-only and update first-20/topology docs;
- keep dual route only with explicit New Game vs Load Game proof cards.

Until resolved, route proof status is `PENDING VERIFICATION`.
