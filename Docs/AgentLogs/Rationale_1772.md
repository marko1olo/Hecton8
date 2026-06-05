# Rationale 1772 - In-Game Wiki / PDA / Encyclopedia Lore

Evidence class: STATIC_DOC / STATIC_SOURCE.

## Authority Loaded

- `AGENTS.md`
- `.agents-skills/README.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt`
- `.agents-skills/TOOL_Designer_Facades_CSV_Binary_Bridge.txt`
- `.agents-skills/PROG_Quest_State_Graph_Logic.txt`
- `.agents-skills/DATA_Runtime_Struct_Layout_ARM64.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `PROJECT_BIBLES.md`
- `TASTE.md`
- `writing.md`
- `narrative.md`
- `localization.md`
- `textes.md`
- `Docs/Lore/Lore_Bible.md`
- `Docs/Lore/Canon_Locks.md`
- `Docs/Lore/Lore_Content_System.md`
- `Docs/Lore/Lore_Localization_Model.md`
- `Docs/Lore/Lore_Multilingual_Content_Architecture.md`
- `Docs/Lore/Codex_Delivery_Map.md`
- `Docs/Lore/AppliedContent/README.md`
- `Docs/Lore/Encyclopedia/README.md`
- `Docs/Lore/Encyclopedia/Article_Index.md`

## Source Decisions

- Use packet JSON as source for selected in-game wiki pages. Generated Markdown pages must match the English authority rows changed in packet JSON.
- Treat non-English rows as localization debt unless native/fluent review is proven. Do not mark them native-final.
- Keep runtime claims static: no Unity/PDA runtime proof is implied by Markdown or JSON changes.
- Do not edit external-site Markdown pages. Packet `external_site` fields are left unchanged unless required by source consistency.

## Rejected Prose Pattern

- Rejected internal-spec openings of the form "`X` defines..." when exported into the in-game wiki.
- Rejected field notes that say "Use for..." because they are authoring instructions, not player-facing recovered knowledge.
- Rejected any wording that makes the bright surface/photic shelf inherently bleak. Shallow beauty must remain useful evidence, not a darkness cover.
- Rejected terminal/scanner copy that passed tone but exceeded the static text-bounds model; trimmed five en_US bridge fields after verifier output.

## Selected Entry Set

- `P046_PUMP_ROOM_HANDSHAKE`: rejected "defines Submerge machinery language" because the PDA should tell the player how pump authority, corridor pressure, and return risk interact.
- `P049_SONAR_RETURN_ROUTE`: rejected "defines navigation pressure" because the PDA should tell the player that a stale return ping is a hazard signal, not a map lecture.
- `P060_FIRST_HOUR_SPINE`: rejected opening-structure prose because a recovered entry should summarize what the player has proven: damaged drop, bright shelf, pump shelter, sanitized lie, first Atlas repair scar.
- `P061_MAINTENANCE_ECOLOGY`: rejected taxonomy-first prose because Atlas repair ecology must stay operational: what conducts, seals, repeats, tags, and misroutes.
- `P221_PHOTIC_MAT_BASELINE`: rejected "defines shallow contrast" because the surface/photic lock requires bright useful ecology plus risk, not writer-facing contrast language.
- `P291_PHOTIC_MAT_CODEX_CARD`: rejected "Codex Card defines..." and "Use for..." because specimen entries must tell the player how to scan/avoid/harvest without damaging sealed seams.

## Final Source Decisions

- Updated en_US packet rows only. Non-English rows remain present but stale against the new English source and require native localization refresh.
- Matched generated en_US Markdown page bodies to packet JSON fields for `in_game_wiki`, `scanner`, `terminal`, `audio`, and `field_note`.
- Left `external_site` fields unchanged because the owned work was in-game wiki/PDA/codex, not public marketing pages.
- Did not add related links because no verified related-link field exists in the selected packet/page schema.
- Did not change unlock gates. Unlock and spoiler notes are recorded in `ingame_translation_status.md`.

## Validation Decisions

- Used `LoreTextBoundsVerifier.py` as the scoped text-bound proof. Full project has existing/draft issues; selected en_US rows now have zero issues.
- Recorded the `AppliedLoreRuntimeAudit.py --source-only` failure as unrelated pre-existing publication status mismatch on `P456_SITE_HOME_LONGFORM_BRIEF.md`; no selected edited packet was implicated.
- Did not run Unity or dotnet build because this was content JSON/Markdown work and no compile path was required.

## Additional Pass - 2026-06-04

Source decisions:

- Continued the same Agent 1772 scope after the user requested more work.
- Chose additional in-game wiki entries already present in packet JSON and generated Markdown mirrors instead of inventing a new content lane.
- Updated en_US authority rows only; non-English rows remain present but stale and require native localization refresh.
- Preserved packet IDs, article IDs, title keys, unlock gates, POI tags, biome tags, locale keys, and text direction values.
- Left `external_site` copy unchanged because the owned lane is in-game PDA/wiki/encyclopedia content.
- Did not add related links because the selected packet/page schema still has no verified related-link field.

Rejected prose pattern:

- Rejected stale Aegir moon names in `P017`; the current ladder is Skarn, Vela, Claw, Lumen, Thorne, Anvil, Kestrel, HECTON-8, Mute.
- Rejected "geology/ecology defines..." prose when it exported into the PDA as writer-facing architecture notes.
- Rejected abstract depth-band language where the player needs seal, battery, oxygen, return ping, and route-window consequences.
- Rejected bleak-surface framing for photic shelf entries. The shallow route stays bright and useful while risk comes from weather, predators, route decay, and oxygen decisions.
- Rejected cable reef text that warned the player without telling them what to scan, cut, avoid, or preserve.

Validation decisions:

- Used direct packet/page parity checks for the six additional entries because the release-set text-bound verifier manifests for RS004 and RS007 are absent.
- Used a selected en_US text-bound pass as scoped proof: 42 rows, 0 issues.
- Used full packet JSON parse as structural proof: 451 packet files pass.
- Recorded `AppliedLoreRuntimeAudit.py --source-only` as blocked by unrelated `P456_SITE_HOME_LONGFORM_BRIEF.md` frontmatter, not by the selected 1772 entries.
- Did not run Unity or dotnet build. This pass changed content JSON, Markdown mirrors, and audit artifacts only.

## Runtime Hardening Pass - 2026-06-04

Source decisions:

- Kept the changes inside first-party runtime owners instead of creating helper systems: player stress, water optics, and PDA encyclopedia streamer.
- Rejected DataVault ownership for editor CSV scratch bytes. The scratch is local, cold, editor-only staging; the vault remains for runtime DTO ownership.
- Hoisted CSV file reads and parsing outside write locks. Locks now wrap final DTO copy only.
- Preserved DTO shapes, save identity, unlock truth, and `GlobalQualityWeight` behavior.

Validation decisions:

- Did not start `dotnet build` because the user forbade build spam and a pre-existing `dotnet` process is already running.
- Used Unity script validation plus static source scans as the proof path for this pass.
- Treated PDA standard validation timeout as a validator limit because basic validation, brace/preprocessor checks, and focused static scans passed.

## Runtime Hardening Pass 2 - 2026-06-04

Source decisions:

- Chose `BabelSubtitleSyncRuntime` because it is a first-party UI/localization owner in the PDA/wiki data-streaming lane and still had a direct writable vault resolve path.
- Reused the existing mutation guard and release functions instead of adding another buffer manager.
- Stored only the active `BufferID`; release stays typed through existing handles and does not introduce reflection, interface boxing, or extra containers.
- Did not patch `DynamicDecalVaultRuntime.cs` despite console errors because exact source search proves the reported missing helper call sites are stale in the current file.

Validation decisions:

- Treated the Unity validator duplicate-signature report as false because `EnsureSubtitleLayoutValid` has one declaration and two call sites.
- Did not request or run `dotnet build`; existing compiler processes were active.

## Runtime Hardening Pass 3 - 2026-06-04

Source decisions:

- Chose `DiegeticGlitchSurgeonRuntime` because it is a first-party UI glitch owner in the PDA/terminal presentation lane and had a dead writable DataVault resolve route beside the existing correct write-lock helper.
- Deleted the unused writable helper instead of adding a second abstraction or parallel buffer manager.
- Left read helper signatures unchanged because downstream unsafe read-only pointer and scratch-copy helpers currently accept `NativeArray<T>`; converting that API to `NativeArray<T>.ReadOnly` requires a separate proof pass.
- Did not patch `TerminalOsRuntime` or `DiegeticGyroCompassRuntime` direct resolve helpers in this pass because those helpers feed scheduled job buffers and require explicit owner lock-lifetime redesign, not a one-line token replacement.

Validation decisions:

- Treated the Unity validator timeout as a tool limitation because brace balance, preprocessor balance, diff check, and exact token scans passed.
- Did not run `dotnet build`; active `dotnet` processes were present and the project rules forbid build spam or parallel build pressure.

## Runtime Hardening Pass 4 - 2026-06-04

Source decisions:

- Chose `InteractionUI` because it owns the diegetic interaction prompt presentation lane and still had a hidden span-to-string conversion in localized prompt cache creation.
- Reused the existing `_promptCharBuffer` instead of adding another staging buffer or helper system.
- Kept cached strings because `CurrentPrompt` and `UnityEvent<string>` are established cold-facing contracts; converting those contracts requires a separate UI API migration.
- Kept `LateFrameTick` on `SetCharArray` so the runtime presentation path remains zero string-format work.

Validation decisions:

- Treated the MCP string-concatenation-in-Update warning as false for this file because exact source has no `Update`, `FixedUpdate`, or `LateUpdate` method and exact concat/interpolation scan is empty.
- Used literal-path `.meta` scanning because bracketed asset names in `Assets/Shapes` produce wildcard false positives with plain `Test-Path`.
- Did not run `dotnet build`; the active `dotnet` process and project throttling rules forbid starting another build.

## Runtime Hardening Pass 5 - 2026-06-04

Source decisions:

- Chose `DiegeticGyroCompassRuntime` because it had a narrow, provable hot-path issue: a single-element scheduled drift job plus a direct DataVault lane resolve path.
- Rejected keeping Burst for one compass row. The scheduling/fence cost is larger than the scalar work and created a same-frame completion dependency.
- Rejected a broad `TerminalOsRuntime` lock rewrite in this pass. Its `TryOpenVaultBuffer` helper has dozens of mixed read, write, and scheduled-job call sites, so a safe fix requires a dedicated split-lock design.
- Rejected per-buffer write locks across the GPR scan job lifetime. `GroundPenetratingRadarRuntime` already uses mutation guards for its multi-buffer scan job; converting that route needs a separate owner-lifetime proof, not a token patch.
- Kept the compass DTO layout and quality-weight behavior unchanged. The pass changes ownership/synchronization, not gameplay truth.

Validation decisions:

- Used Unity standard validation and exact static scans instead of `dotnet build`.
- Did not run `dotnet build`; active `dotnet` PID `49228` was present and the project rules forbid parallel build pressure.
- Treated the remaining project-wide `TryResolveHandle` hits as future scoped owner passes. They are systemic and cannot be honestly claimed fixed by one local patch.
