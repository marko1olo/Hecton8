# Verification And Docset Reality

Date: 2026-05-07
Status: PENDING VERIFICATION

Purpose:
- describe how the project currently proves things to itself
- separate real verification surfaces from optimistic documentation
- explain what is implemented as runtime smoke infrastructure versus what is covered by formal tests

## The Project Does Verify Itself

That part is real.

Evidence in code:
- `ShellVerificationRuntimeSmokeTester`
- `PauseSystemVerifier`
- `StateRecoveryVerifier`
- `SceneTransitionVerifier`
- `BuilderRuntimeSmokeTester`
- `BarterRuntimeSmokeTester`
- `FabricationRuntimeSmokeTester`
- `FieldToolRuntimeSmokeTester`
- `SaveSystemRuntimeSmokeTester`
- `ScanRuntimeSmokeTester`
- `ToolRuntimeSmokeTester`
- `ToolTrialRangeRuntimeSmokeTester`
- `UIRuntimeSmokeTester`
- `WorldGenerativeGeologyRuntimeSmokeTester`

Interpretation:
- the project has built a bespoke runtime-verification culture
- this is not a repo with zero self-checking
- `PhysicalInteractionRuntimeVerifier`, `MantaAcousticRuntimeVerifier`, and `WeakToolsRuntimeSmokeTester` are historical/removed names in the current filesystem, not current evidence.

## But The Verification Mix Is Uneven

What is strong:
- custom smoke/verifier classes exist across many domains
- validation logic is embedded close to the systems it exercises
- Archivarius documentation is actively trying to record current truth

What is weak:
- formal automated Unity test inventory is tiny
- bespoke runtime verifiers are not the same thing as stable CI-grade regression coverage
- many docs are source-backed but still time-sensitive

## Actual Verification Surface Types

### 1. Runtime Smoke Tools

Strength:
- broad
- domain-specific
- likely useful for local validation

Weakness:
- harder to make authoritative over time
- easier to drift with scene/setup assumptions

### 2. Editor Validators / Authoring Guards

Evidence:
- content sanity and bootstrap authoring utilities
- `ContentSanityValidator`
- performance validators and compile gates in editor code

Strength:
- project is trying to catch authoring problems before play

Weakness:
- editor tooling does not replace runtime proof

### 3. Archivarius Reports

Strength:
- unusually rich internal documentation layer
- same-day reports already narrow architecture drift, bootstrap truth, save participants, content identity

Weakness:
- this docset can age within hours when active files are changing quickly
- some older accusations were already stale and required reread

## Recent Archivarius Areas That Match Current Static Truth

These recent reports align well with current code reads:
- `2026-04-30_SERVICE_AUTHORITY_DRIFT.md`
- `2026-04-30_BOOTSTRAP_RUNTIME_AUTHORITY_TRUTH.md`
- `2026-04-30_SAVE_PARTICIPANT_LEDGER.md`
- `PROJECT_CONTENT_LEDGER.md`

Why they matter:
- they already capture uncomfortable middle-state truths
- they are more useful than broad aspirational architecture notes

## Current Verification Hierarchy

Most trustworthy in this static pass:
1. current source code
2. narrow same-day Archivarius reports that cite current source directly
3. broader older docs
4. thin automated test inventory

## What This Says About The Project

Positive reading:
- the team knows the project is complicated
- the repo contains real introspection and self-audit effort

Negative reading:
- the need for this many bespoke verifiers is itself evidence that the runtime is hard to reason about
- verification maturity is broad but fragmented

## Readiness Judgment

Verification culture:
- 71%

Formal regression confidence:
- 18%

Docset usefulness:
- 78%

Docset stability:
- 54%

## Brutal Reading

The project is not under-documented.
It is under-normalized.

There is enough evidence to study the project seriously.
There is not yet enough verification discipline to trust every large subsystem just because it has a document and a smoke tester.
