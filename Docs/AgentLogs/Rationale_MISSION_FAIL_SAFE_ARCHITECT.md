# Rationale_MISSION_FAIL_SAFE_ARCHITECT

Status: SCENARIO STABILIZED - PENDING UNITY VERIFICATION / ACTIVE BATCH DRIFT DETECTED
Evidence Class: STATIC_DOC unless explicitly upgraded by command output.

## Decision 001 - Batch Source Fallback

Problem: User requested CURRENT_BATCH_OSHINO.md, but workspace recursive scan found no file with that name. Active task prompt exists in Docs/Tasks/CURRENT_BATCH.md.
Solution: Extracted the exact `<AGENT_PROMPT id="MISSION_FAIL_SAFE_ARCHITECT">` block from Docs/Tasks/CURRENT_BATCH.md using CLI `Select-String`.
Rejected Alternatives: Waiting for a missing file would stall the batch; using neighboring prompts would violate strict parsing.
Scalability potential: No runtime impact.
Hardware Impact: 0 us runtime. Documentation-only routing.

## Decision 002 - Task Count Interpretation

Problem: Prompt header says "15 TITANIUM TASKS" but the XML block contains six numbered actionable tasks.
Solution: Treat task count as 6 because the identification rule asks for total tasks in the XML tag, not the inconsistent section label.
Rejected Alternatives: Reporting 15 would invent nine absent tasks.
Scalability potential: No runtime impact.
Hardware Impact: 0 us runtime. Prevents scope hallucination.

## Decision 003 - Documentation-Only Execution Boundary

Problem: Assignment asks to audit quest DAG and write tooltips, not implement runtime code.
Solution: Keep edits in Docs/Design/Missions and agent status/log files unless existing mission data proves a direct data edit is required.
Rejected Alternatives: Creating runtime quest classes or new EventIDs; public API churn during parallel batch is prohibited.
Scalability potential: Authored fail-safes preserve low-tier cheap graph evaluation and allow high-tier presentation overkill without changing quest truth.
Hardware Impact: Estimated 0 us hot-path change; any future runtime use must remain baked hash/flag driven.

## Decision 004 - Missing Meta Campaign Status

Problem: The prompt required `Status_META_CAMPAIGN_DIRECTOR.md` and "20+ variables", but the status file is absent. Current `MetaCampaignService` source exposes four globals, not an outpost-specific variable set.
Solution: Documented the absence as a source boundary and authored 32 explicit outpost DAG flags in `Docs/Design/Missions/Outpost_Failure_Modes.md`.
Rejected Alternatives: Pretending the missing file exists; mutating `MetaCampaignService` during a documentation/logic prompt; creating new single-use event IDs.
Scalability potential: Low tier evaluates one compact mission branch; High/Ultra can add richer presentation while keeping the same flag truth.
Hardware Impact: 0 us runtime from this doc. Future runtime implementation should remain O(1) bit checks.

## Decision 005 - Ghost Power As Mission-Only Reserve Bus

Problem: Current WFC outpost source has no `Generator` cell kind, so a mission that requires a generated Generator room can soft-lock.
Solution: Defined Ghost Power as a deterministic quest/DAG fallback flag that powers only the relay, door, terminal, optional scrubber, and entry marker.
Rejected Alternatives: Forcing the WFC solver to guarantee a Generator room; publishing false full-grid generation; spawning a new room at runtime.
Scalability potential: Low = static panel glow and relay tick. Middle = brownout flicker. High = wet spark VFX. Ultra = richer arcing/audio/CRT decay. Quest truth remains unchanged.
Hardware Impact: Estimated steady-frame cost 0 us until runtime implementation. Intended future path avoids full power solve changes and should be a local flag check plus dirty visual update.

## Decision 006 - Gas/O2 Safety Boundaries

Problem: Power repair text can accidentally imply scrubbers produce oxygen or force the player to read logs inside unsafe rooms.
Solution: Bound mission constraints to `GasDynamicsSolver` constants: CO2 warning around 100 seconds in an unpowered sealed room, fire room excluded from critical path, breached/submerged rooms excluded from critical path, scrubber treated as CO2 removal only.
Rejected Alternatives: Treating Ghost Power as oxygen production; requiring fire/breached room objectives for tension.
Scalability potential: Low tier avoids expensive gas simulation by using scalar room flags. High/Ultra can buy better alarms, fog, panel decay, and audio without changing gas truth.
Hardware Impact: Documentation-only. Future runtime should use existing scalar gas flags and avoid any particle/fluid/gas simulation.

## Decision 007 - Compile Guard Blocked By Missing Dotnet

Problem: Required compile guard could not execute because `dotnet` is not recognized in the current shell environment.
Solution: Recorded the exact command and failure in the status ledger. Since only markdown/status/log files changed, source compilation is not affected by this pass, but compile proof remains absent.
Rejected Alternatives: Claiming compile success from static docs; trying unrelated destructive environment changes.
Scalability potential: No runtime impact.
Hardware Impact: 0 us runtime.

## Decision 008 - Exact Log Text Instead Of Summaries

Problem: A summaries-only lore table left ambiguity about what the authored Marauder logs actually say.
Solution: Replaced summaries with exact log strings and kept a required physical-state column for `InternalFire`, `Breached`, `ScrubberInstalled`, Ghost Power/generator state, and exit marker state.
Rejected Alternatives: Leaving summaries for a later narrative pass; that would fail the prompt's lore-consistency requirement.
Scalability potential: Static strings can be localized/baked once. High-tier presentation can add voice, panel decay, and audio treatment without changing mission flags.
Hardware Impact: 0 us runtime from this doc. Future runtime must load these through hashed localization keys, not per-frame literals.

## Decision 009 - Handoff JSON Instead Of Partial Runtime Localization Patch

Problem: The tooltip/log payload needs to be actionable, but patching only the active English localization table would leave generated `LocKeys` and translated tables stale. The earlier assumed `Data/Localization/en_US.json` path is not present in this workspace; the active observed source is `Assets/_Project/Scripts/English.json`.
Solution: Added `Docs/Design/Missions/Outpost_FailSafe_Handoff.json` with exact strings, trigger/suppress flags, fallback constraints, gas constraints, and `LocHash.Compute`-compatible FNV hashes.
Rejected Alternatives: Editing generated localization keys by hand; patching English-only runtime data; editing ScriptableObject YAML for quest assets without Unity API validation.
Scalability potential: Low tier can consume the same baked hash keys with no runtime strings. High/Ultra can add richer voice, panel animation, and VFX against the same static keys.
Hardware Impact: 0 us runtime from this documentation pass. Future integration must be a bake/import step, not per-frame string lookup or English-only runtime patching.

## Decision 010 - Canonical Flag Vocabulary After Self-Review

Problem: The prose mission doc and JSON handoff drifted on several flag names: `*_found` versus `*_read`, `claim_complete` versus `mission_complete`, and different deadlock/save-restore labels. That would create implementation ambiguity for a future quest baker.
Solution: Canonicalized both files to the same 32 outpost flags, including five explicit Marauder log commit flags, fire/breach observation flags, flood/fire fail-safe flags, `outpost.deadlock_revert_requested`, `outpost.state_restored_from_save`, and `outpost.mission_complete`.
Rejected Alternatives: Keeping the 30-flag count to preserve the earlier report; accepting alias names; leaving the mismatch for a runtime integrator.
Scalability potential: Low tier gets one stable bit vocabulary for O(1) checks. High/Ultra can add presentation systems without renaming quest truth.
Hardware Impact: 0 us runtime from this documentation pass. Future runtime risk is reduced because hash constants can be generated from one canonical vocabulary.

## Decision 011 - Editor-Only Handoff Validator

Problem: Manual PowerShell/static checks proved the handoff once, but nothing in the Unity authoring workflow would stop a future edit from reintroducing stale aliases, wrong hashes, missing commit flags, or prose/JSON drift.
Solution: Added `Assets/_Project/Scripts/Editor/OutpostFailSafeHandoffValidator.cs`, an editor-only menu validator for `Docs/Design/Missions/Outpost_FailSafe_Handoff.json` and `Docs/Design/Missions/Outpost_Failure_Modes.md`.
Rejected Alternatives: Runtime validation in quest code; direct localization table mutation; integrating a partial quest baker without Unity compile proof; expanding public quest interfaces during the batch.
Scalability potential: Low tier receives baked constants from a validated source instead of runtime string checks. High/Ultra can layer richer outpost presentation on the same validated flag vocabulary.
Hardware Impact: 0 us player runtime. The validator is editor-only and cold-path; all managed allocations are outside gameplay and marked as `COLD ALLOC`.

## Decision 012 - Canonical Cold Allocation Comments

Problem: Self-review found the new validator used ASCII hyphen separators in `COLD ALLOC` comments, but the project mandate requires the exact long-dash separator format.
Solution: Corrected all 12 editor-only allocation comments in `OutpostFailSafeHandoffValidator.cs` to the canonical format.
Rejected Alternatives: Keeping ASCII-only comments and falsely reporting compliance; removing allocation comments from editor-only code.
Scalability potential: No player runtime impact. The correction improves static audit accuracy for future import/bake work.
Hardware Impact: 0 us player runtime. Editor-only documentation/comment fix.

## Decision 013 - Full Topological Coverage Gate

Problem: Self-review found `Outpost_FailSafe_Handoff.json` declared 32 mission flags but listed only 23 in `topologicalOrder`. That makes the graph handoff partial while the status claimed a 32-flag DAG.
Solution: Expanded `topologicalOrder` to include every declared outpost flag exactly once and upgraded `OutpostFailSafeHandoffValidator.cs` to reject missing topological coverage.
Rejected Alternatives: Leaving topological order as a critical-path-only summary was rejected because the field name is machine-readable graph authority. Moving the missing flags into prose only was rejected because the validator would still miss drift.
Scalability potential: Low tier gets deterministic O(1) bit progression with no hidden branch flags. High and Ultra can add richer branch presentation without changing quest truth.
Hardware Impact: 0 us player runtime. Editor-only validation catches data drift before localization/quest bake.

## Decision 014 - Gas Room Flag Vocabulary Gate

Problem: Self-review found `Outpost_FailSafe_Handoff.json` used invented `roomflag.*` tokens and a bare `Submerged` condition. Current source authority defines `GasDynamicsRoomFlags.InternalFire`, `Breached`, `ScrubberInstalled`, and `Occupied`; submerged state is a scalar `roomSubmerged01`, not an enum flag.
Solution: Canonicalized JSON physical-state references to `GasDynamicsRoomFlags.*` and `roomSubmerged01`, then upgraded `OutpostFailSafeHandoffValidator.cs` to reject legacy `roomflag.*`, unsupported gas enum values, and bare `Submerged` flag claims.
Rejected Alternatives: Leaving the physical-state consistency contract as prose only; inventing a `GasDynamicsRoomFlags.Submerged` member; changing the runtime gas interface during a scenario-design task.
Scalability potential: Low tier keeps scalar gas checks cheap. High and Ultra can add stronger visual warnings for fire, breach, or submerged-fraction rooms without changing mission truth.
Hardware Impact: 0 us player runtime. Editor-only validation prevents invalid gas vocabulary before quest/localization bake.

## Decision 015 - Static Editor Array Allocation Comments

Problem: The validator used editor-only static string arrays for gas allowlists and stale-token needles without explicit cold-allocation ownership comments.
Solution: Added canonical `COLD ALLOC` comments for those arrays in `OutpostFailSafeHandoffValidator.cs`.
Rejected Alternatives: Ignoring static editor arrays because they are not runtime hot-path; that weakens allocation review discipline and makes future scans less exact.
Scalability potential: No player runtime impact. Cleaner editor validation ownership supports safer future bake/import hardening.
Hardware Impact: 0 us player runtime. Comment-only compliance correction in an editor-only file.

## Decision 016 - Validator Self-Fail Token Removal

Problem: The editor validator rejects the legacy room-flag namespace token, but `Outpost_Failure_Modes.md` contained that exact forbidden literal while explaining the rejection rule. Because the validator scans the prose document, the menu validation would fail on its own documentation.
Solution: Reworded the prose to describe legacy room-flag namespace tokens without embedding the banned literal.
Rejected Alternatives: Weakening `OutpostFailSafeHandoffValidator` was rejected because stale-token detection is the point of the gate. Ignoring the prose document was rejected because prose/JSON drift is a known failure mode.
Scalability potential: Low/Middle/High/Ultra all keep one stable mission flag vocabulary; invalid legacy tokens fail before bake.
Hardware Impact: 0 us player runtime. Documentation-only correction prevents editor validation noise.

## Decision 017 - Hash Collision and Bare Gas Token Gate

Problem: Duplicate strings are not the only authored-data failure mode; two different outpost flags or localization IDs could collide after `LocHash.Compute`, and a future handoff could reintroduce bare `InternalFire`, `Breached`, `ScrubberInstalled`, or `Occupied` tokens without the required `GasDynamicsRoomFlags.` prefix.
Solution: Confirmed `OutpostFailSafeHandoffValidator.cs` validates mission flag hash uniqueness, localization hash uniqueness, and bare gas enum token rejection. Re-ran static JSON validation against the current handoff.
Rejected Alternatives: Relying on string uniqueness only; accepting bare gas tokens as designer shorthand; deferring collision detection to runtime localization or quest bake.
Scalability potential: Low tier gets deterministic hash tables without collision ambiguity. High and Ultra can add richer outpost presentation while consuming the same validated keys.
Hardware Impact: 0 us player runtime. Editor-only validation prevents invalid bake inputs.

## Decision 018 - Metadata and Hash Contract Gates

Problem: The handoff JSON carried source metadata, runtime asset decision notes, and hash-contract constants, but the editor validator did not enforce those fields. A future edit could silently claim the wrong evidence class, source batch, localization table, or hash algorithm.
Solution: Added metadata validation, runtime asset decision validation, and hash-contract validation to `OutpostFailSafeHandoffValidator.cs`, including checks against `LocHash.FnvOffsetBasis` and `LocHash.FnvPrime`.
Rejected Alternatives: Treating metadata as human-only prose; relying on later quest/localization bake failures; duplicating hash constants without comparing them to the runtime source.
Scalability potential: Low tier and High/Ultra consume the same baked hashes. Preventing contract drift keeps runtime lookup O(1) and avoids fallback string paths.
Hardware Impact: 0 us player runtime. Editor-only validation catches invalid handoff metadata before bake.

## Decision 019 - Active Batch Drift Boundary

Problem: Current `Docs/Tasks/CURRENT_BATCH.md` no longer contains `<AGENT_PROMPT id="MISSION_FAIL_SAFE_ARCHITECT">`. The working-tree diff shows the Mission Fail-Safe prompt block was removed and replaced by later batch prompts, while this agent's status/log files still exist.
Solution: Marked the drift in this agent's status and log instead of continuing Mission Fail-Safe implementation from stale prompt authority.
Rejected Alternatives: Reverting another agent's `CURRENT_BATCH.md` changes; switching identities to a different active prompt without user assignment; claiming live prompt re-extraction still passes.
Scalability potential: No runtime impact. The boundary prevents stale mission data from being treated as a live batch source by later bake/import work.
Hardware Impact: 0 us player runtime. Documentation-only hygiene correction.

## Decision 020 - Source Authority Gate

Problem: The handoff JSON still named `Docs/Tasks/CURRENT_BATCH.md` as `sourceBatch`, but the editor validator only checked that the field contained the expected path. It did not verify whether that file currently contained the Mission Fail-Safe prompt.
Solution: Added `sourceAuthority` metadata to the handoff and upgraded `OutpostFailSafeHandoffValidator.cs` to read the source batch, check expected prompt ID/role presence, and require either `ACTIVE_BATCH_MATCHED` or `ACTIVE_BATCH_DRIFT_DETECTED` according to actual file contents.
Rejected Alternatives: Trusting source-batch metadata as prose; failing every validation when a later batch replaces `CURRENT_BATCH.md`; reverting concurrent batch changes.
Scalability potential: No player runtime impact. Future quest/localization bake tooling can reject stale prompt authority before generating runtime hashes or tables.
Hardware Impact: 0 us player runtime. Editor-only static validation; no gameplay path changed.

## Decision 021 - Prose Source Boundary Gate

Problem: `Outpost_Failure_Modes.md` still said the prompt was actively extracted from `Docs/Tasks/CURRENT_BATCH.md`, while the current batch no longer contains the Mission Fail-Safe prompt. JSON had the drift marker, but prose still looked live.
Solution: Reworded the source boundary to distinguish historical extraction from current `ACTIVE_BATCH_DRIFT_DETECTED` status, and upgraded `OutpostFailSafeHandoffValidator.cs` to reject stale live-source wording in the prose document.
Rejected Alternatives: Updating JSON only; leaving prose as historical-but-ambiguous; relying on human review to catch the mismatch.
Scalability potential: No player runtime impact. Authoring/bake consumers now get one consistent source-authority story before generating runtime data.
Hardware Impact: 0 us player runtime. Editor-only static validation; no gameplay path changed.

## Decision 022 - Gas Constant Drift Gate

Problem: `Outpost_FailSafe_Handoff.json` stored `fireOxygenDrainKpaPerSecond` as `0.4`, but `GasDynamicsSolver` source defines `DefaultFireO2KPaPerSecond = 0.080f`. The prose uses `0.080 kPa/s * 5 = 0.400 kPa/s` as an aggregate multi-room note, but the JSON field is a per-room solver default.
Solution: Corrected the JSON value to `0.08` and upgraded `OutpostFailSafeHandoffValidator.cs` to exact-check the gas constants against the current solver defaults.
Rejected Alternatives: Keeping positivity-only validation; treating aggregate prose math as the per-room JSON default; changing `GasDynamicsSolver` constants from a scenario-design task.
Scalability potential: Low tier keeps cheap scalar gas checks with source-matched defaults. High/Ultra presentation can still add stronger warnings without changing gas truth.
Hardware Impact: 0 us player runtime. Editor-only static validation; no gameplay path changed.

## Decision 023 - Headless Static Validator

Problem: Unity, dotnet, msbuild, and csc are absent from this environment, and repeated `rg`/`git` shell wrappers time out under load. The handoff had strong static checks, but most were ad hoc command snippets rather than a reusable artifact.
Solution: Added `Tools/OutpostFailSafeValidate.py`, a deterministic offline validator for the Mission Fail-Safe handoff/prose/source contract.
Rejected Alternatives: Continuing to rely on fragile shell one-liners; claiming Unity/editor validation without the toolchain; adding runtime validation to gameplay code.
Scalability potential: No player runtime impact. Future bake/import work can run the tool before producing runtime hashes or localization tables.
Hardware Impact: 0 us player runtime. Offline Python validation only.

## Decision 024 - Headless Semantic Validator Hardening

Problem: The first headless validator pass proved counts, hashes, gas constants, and stale tokens, but a bad edit could still preserve counts while renaming a tooltip, swapping a Marauder log, pointing a trigger to an undeclared flag, or deleting the oxygen-production warning.
Solution: Hardened `Tools/OutpostFailSafeValidate.py` to enforce exact tooltip/log locId order, Narrative layer/text presence, declared trigger/suppress/commit refs, declared required-state refs, fallback risk order, fallback setFlag validity, all `outpost.*` tokens in JSON/prose, and the gas rule phrase `does not create oxygen`.
Rejected Alternatives: Leaving semantic validation to human review; accepting count-only validation; moving the check into runtime quest code.
Scalability potential: No player runtime impact. Low-tier runtime still receives static baked data only; High/Ultra presentation can build richer panels/log playback on top of the same validated IDs.
Hardware Impact: 0 us player runtime. Offline Python validation only; no Unity hot path changed.
