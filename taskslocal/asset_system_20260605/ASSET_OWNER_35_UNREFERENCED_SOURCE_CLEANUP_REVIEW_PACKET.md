# ASSET_OWNER_35_UNREFERENCED_SOURCE_CLEANUP_REVIEW_PACKET

ID: `ASSET_OWNER_35_UNREFERENCED_SOURCE_CLEANUP_REVIEW_PACKET_WRITER`
Role: Future-owner static cleanup review packet for unreferenced source assets.
Project: `C:\hades\Hecton8`
Status: `STATIC / PENDING VERIFICATION`
Evidence class: `STATIC_DOC`, `STATIC_SOURCE`

## Objective

Audit unreferenced source candidates without deleting or mutating assets. Convert the 2026-06-05 GUID triage into a safe future-owner review queue that separates useful source candidates, quarantine candidates, Unity readback needs, visual/source QA needs, and deletion candidates that remain blocked until proof exists.

This packet is directly distributable. It is not deletion authorization.

## Evidence Basis

Use only these source artifacts unless a task below explicitly requires a narrow follow-up read:

- `Docs/AssetAudit/ASSET_GUID_UNREFERENCED_SOURCE_TRIAGE_20260605.md`
- `Docs/AssetAudit/ASSET_GUID_UNREFERENCED_SOURCE_TRIAGE_20260605.csv`
- `Docs/AssetAudit/ASSET_GUID_REFERENCE_MATRIX_20260605.md`
- `Docs/AssetAudit/ASSET_GUID_REFERENCE_MATRIX_20260605.csv`
- `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_REJECTION_20260605.md`
- `Docs/AssetAudit/ASSET_NEXT_ACTION_BOARD_20260605.md`

Known static facts:

- Unreferenced triage rows: `3488`.
- Largest buckets: `VENDOR_OR_LEGACY_UNREFERENCED_REVIEW` = `2203`, `UNREFERENCED_STATIC_SOURCE_REVIEW` = `594`, `PREFAB_UNUSED_SOURCE_REVIEW` = `226`, `SOURCE_PROXY_PLACEHOLDER_QUARANTINE_REVIEW` = `162`.
- First-party unreferenced rows: `1285`.
- Non-project asset path rows: `1951`.
- Rows >= 8 MB source size: `31`.
- Current product-face visuals are rejected and runtime proof packet is absent.

Evidence boundary:

- Static GUID absence proves only that selected serialized reference files did not contain the GUID token.
- It does not prove Unity import state, Addressables labels, SpriteAtlas packing, AssetBundle membership, runtime code loading, reflection use, Resources use, visual quality, audio quality, memory residency, or safe deletion.
- All review decisions from this packet stay `PENDING UNITY READBACK`, `PENDING VISUAL QA`, `PENDING AUDIO QA`, or `PENDING ROUTE PROOF` unless a later owner produces proof artifacts.

## Authority Docs And Mandates

Follow:

- `AGENTS.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

Route bibles for future mutation owners only:

- Texture/material/import owners: `rendering.md`, `shaders.md`, `streaming.md`, `performance.md`
- Audio owners: `audio.md`, `streaming.md`, `performance.md`
- Mesh/prefab owners: `3dmodel.md`, matching generated asset bible, `rendering.md`, `performance.md`
- Visual product-face owners: `TASTE.md`, `VISION_LOCKS.md`, matching domain bible

Read `HECTON8_ORCHESTRATOR.md` only if the future owner is explicitly assigned controller/orchestration work. An ordinary cleanup-review owner must not read it.

## Owned Scope For This Future Owner

Allowed:

- Read the listed audit files.
- Produce review tables or notes under `Docs/AssetAudit/` only if explicitly assigned by the controller.
- Classify rows into the five lanes below.
- Request Unity readback, visual QA, audio QA, import QA, or Addressables review as proof gates.

Forbidden:

- No deletion.
- No moving assets.
- No `.meta` deletion.
- No raw `.prefab`, `.unity`, `.mat`, `.asset`, `.shader`, or importer YAML edits.
- No Unity import, Play Mode, build, or scene save unless a later explicit Unity owner task assigns it.
- No runtime, visual, import, profiler, GC, memory, Addressables, or audio readiness claim from static rows.
- No blind removal of generated textures, Gemini-watermark sources, prototype masks, source images, or third-party/vendor assets.

## Review Lanes

1. `KEEP_AS_CANDIDATE_SOURCE`
   Static-unreferenced source that may still be useful for product-face repair, generated asset improvement, audio iteration, terrain/material authoring, sky/Aegir work, UI atlas work, or offline baking.

2. `QUARANTINE_CANDIDATE`
   Demo, vendor, proxy, placeholder, legacy, duplicate-looking, or non-project-path source that may need isolation from active routes. Quarantine is not deletion. Vendor and third-party integrity rules still apply.

3. `NEEDS_UNITY_REFERENCE_READBACK`
   Any asset where static GUID absence is not enough: Addressables labels, importer state, scene/prefab hidden references, SpriteAtlas membership, shader effective slots, material overrides, Resources usage, or code/runtime loading may exist.

4. `NEEDS_VISUAL_OR_SOURCE_QA`
   Textures, models, materials, sky/water/terrain sources, generated textures, Gemini-watermark sources, audio beds, and UI sources that need human/Unity/source QA before keep/quarantine/delete classification.

5. `DELETION_CANDIDATE_AFTER_PROOF_ONLY`
   Asset appears obsolete after static review, owner route review, Unity readback, visual/audio/source QA, Addressables/import check, and rollback plan. Final deletion requires a separate explicit deletion owner task.

## Mandatory Deletion Gate

An asset may enter `DELETION_CANDIDATE_AFTER_PROOF_ONLY`, but it must not be deleted by this packet.

Before any later deletion:

- Prove no serialized GUID reference in the current matrix or a fresh equivalent scan.
- Prove no Unity importer, Addressables, SpriteAtlas, AssetBundle, Resources, code, editor tooling, or runtime load route.
- Prove no visual/source/audio need against current rejected product-face blockers.
- Prove owner approval for third-party/vendor/legacy folders.
- Delete the asset and matching `.meta` in the same deletion operation.
- After deletion, run an orphan `.meta` scan.
- If deleting `.asset`, `.cs`, `.shader`, prefab, scene, material, texture, model, or audio source, produce a rollback list and Git diff before deletion.
- If any proof is missing, status stays `PENDING`, not deletion-ready.

## Numbered Tasks

1. Read `ASSET_GUID_UNREFERENCED_SOURCE_TRIAGE_20260605.md` and record the static summary, action bucket counts, owner scope counts, and evidence boundary. Acceptance proof: copied counts match the source artifact. Fallback: if the file is missing, mark `BLOCKED_SOURCE_FILE_ABSENT`.

2. Read `ASSET_GUID_UNREFERENCED_SOURCE_TRIAGE_20260605.csv` and verify the row count is `3488`. Acceptance proof: command output or table note. Fallback: if row count differs, label all downstream classification `STALE_STATIC_SCAN`.

3. Read `ASSET_GUID_REFERENCE_MATRIX_20260605.md/.csv` only enough to compare referenced versus unreferenced totals and active-route flags. Acceptance proof: record matrix rows `7420`, referenced `3932`, unreferenced `3488`, active world reachable `630` if still present. Fallback: if mismatched, require a fresh matrix before deletion review.

4. Read `VISUAL_REFERENCE_REJECTION_20260605.md` and extract the active product-face blockers that prevent blind cleanup: water volume, shoreline contact, terrain material truth, Aegir/sky, underwater route density, HUD proof packet. Acceptance proof: blocker list recorded. Fallback: if visual rejection file is missing, assume visual QA is required for all product-face texture/model/material sources.

5. Create a local review worksheet in memory or a controller-assigned output, not in Assets. Map every triage row to one of the five lanes. Acceptance proof: every row has one lane and one reason code. Fallback: unclear rows go to `NEEDS_UNITY_REFERENCE_READBACK`, never deletion.

Checkpoint 1 after Task 5:

- No assets deleted or moved.
- No Unity import or scene mutation performed.
- All current lane assignments are `STATIC / PENDING`.
- Report bucket totals and unresolved source-file problems before continuing.

6. Classify `VENDOR_OR_LEGACY_UNREFERENCED_REVIEW` rows first. Default lane: `QUARANTINE_CANDIDATE`, not deletion. Acceptance proof: count reviewed and third-party scope preserved. Fallback: any Crest, Feel, Plugins, MapMagic, ScifiFacility, or legacy Resources row stays quarantine/readback until owner approval exists.

7. Classify `SOURCE_PROXY_PLACEHOLDER_QUARANTINE_REVIEW` rows. Default lane: `QUARANTINE_CANDIDATE` or `NEEDS_VISUAL_OR_SOURCE_QA` if the source could improve product-face blockers. Acceptance proof: proxy/path reason recorded. Fallback: active-route proxy contamination gets owner handoff, not deletion.

8. Classify first-party large audio sources, including ambient beds and breathing/movement/SFX rows. Default lane: `NEEDS_VISUAL_OR_SOURCE_QA` with audio listening/import review. Acceptance proof: audio rows are not marked deletion-ready from static GUID absence. Fallback: if MusicDirector/player audio routing remains unresolved, keep all plausible source audio.

9. Classify first-party scene rows such as bisect scenes and sandbox scenes. Default lane: `NEEDS_UNITY_REFERENCE_READBACK` plus owner intent review. Acceptance proof: scene rows are not deleted by static scan. Fallback: if a scene name looks diagnostic, still require explicit deletion owner and paired `.meta`.

10. Classify fonts and UI/source textures. Default lane: `KEEP_AS_CANDIDATE_SOURCE` or `NEEDS_UNITY_REFERENCE_READBACK`, especially localization font assets and HUD/UI source candidates. Acceptance proof: no font/UI row is deletion-ready without localization/UI owner review. Fallback: retain fonts until `localization.md` owner approves.

Checkpoint 2 after Task 10:

- Vendor/legacy, proxy, audio, scenes, fonts, and UI sources have lane assignments.
- No row has final deletion approval.
- Record `PENDING AUDIO QA`, `PENDING UNITY READBACK`, and `PENDING VISUAL QA` totals.
- If any deletion-ready label exists, downgrade it to `DELETION_CANDIDATE_AFTER_PROOF_ONLY`.

11. Classify texture rows under sky, water, terrain, rocks, photic shallows, foam/contact, caustics, particles, and generated texture folders. Default lane: `KEEP_AS_CANDIDATE_SOURCE` or `NEEDS_VISUAL_OR_SOURCE_QA`. Acceptance proof: generated textures and Gemini-watermark sources are not blindly rejected. Fallback: watermark debt becomes cleanup QA, not automatic deletion.

12. For every texture candidate >= 8 MB, record whether it is a source file, imported texture, normal candidate, mask candidate, UI source, or unknown. Acceptance proof: large texture source list exists with lane and proof need. Fallback: unknown importer role goes to `NEEDS_UNITY_REFERENCE_READBACK`.

13. Classify material rows. Default lane: `NEEDS_UNITY_REFERENCE_READBACK`, because effective shader slots and material overrides are not proven by static GUID absence. Acceptance proof: material rows require Unity readback before quarantine/delete. Fallback: no raw `.mat` YAML edits.

14. Classify shader, compute, VFX, and render asset rows. Default lane: `NEEDS_UNITY_REFERENCE_READBACK` plus shader owner review. Acceptance proof: no shader deletion candidate without variant/usage/readback proof. Fallback: if shader owner absent, status `BLOCKED_BY_SHADER_OWNER`.

15. Classify model/mesh/prefab rows. Default lane: `NEEDS_UNITY_REFERENCE_READBACK` or `NEEDS_VISUAL_OR_SOURCE_QA`, especially product-face mesh replacement candidates. Acceptance proof: prefabs/models are not treated as obsolete if they can replace built-in primitive/proxy visuals. Fallback: no raw YAML mutation.

Checkpoint 3 after Task 15:

- Texture/material/shader/model/prefab lanes are complete.
- Product-face repair candidates are separated from cleanup debt.
- Large source rows have proof needs, not deletion claims.
- Report any rows blocked by missing importer, owner, or visual evidence.

16. Compare unreferenced rows against `ASSET_NEXT_ACTION_BOARD_20260605.md` P0/P1 priorities. Acceptance proof: active blockers outrank cleanup. Fallback: if a source could contribute to water/foam, flora/coral/kelp, sky/Aegir, terrain/geology, HUD oxygen, or audio remediation, keep it as candidate source until owner QA.

17. Mark all `THIRD_PARTY_CREST` rows as quarantine/readback, not mutation/deletion. Acceptance proof: Crest canonical asset integrity preserved. Fallback: if a Crest material/asset looks unreferenced, require Crest owner readback and no runtime material clone/wrapper.

18. Mark all `THIRD_PARTY_FEEL`, `THIRD_PARTY_PLUGINS`, `NON_PROJECT_ASSETS_PATH`, and `LEGACY_RESOURCES` rows as quarantine/readback unless a controller assigns third-party cleanup. Acceptance proof: no vendor source removal. Fallback: owner approval required.

19. Identify duplicate-looking rows by basename, size, extension, and path family only as `DUPLICATE_REVIEW_STATIC`. Acceptance proof: duplicate candidates are not deletion-ready without binary hash and owner route proof. Fallback: if hashing is not assigned, do not claim duplicate.

20. Identify probable stale diagnostic artifacts, old bisect scenes, sandbox scenes, placeholder/proxy folders, and unused demos as `QUARANTINE_CANDIDATE`. Acceptance proof: isolation recommendation recorded. Fallback: deletion blocked until scene/build/settings/readback proof.

Checkpoint 4 after Task 20:

- Third-party and legacy integrity preserved.
- Active product-face candidates retained.
- Duplicate and diagnostic rows are quarantine candidates only.
- No deletion, no import changes, no Unity claims.

21. Build the future Unity readback request list. Include materials, shaders, prefabs, scenes, Addressables/importer ambiguous rows, fonts, SpriteAtlas/UI candidates, Resources paths, and any row with uncertain runtime/editor usage. Acceptance proof: list has asset path, triage ID, lane, readback question, and required owner.

22. Build the visual/source QA request list. Include generated textures, Gemini-watermark sources, water/foam/contact masks, sky/Aegir candidates, terrain/geology sources, underwater route dressing, flora/coral/kelp sources, UI oxygen source, and model/prefab candidates. Acceptance proof: every row states what screenshot/source/audio comparison is needed. Fallback: if no visual proof exists, status remains `PENDING VISUAL QA`.

23. Build the audio QA request list. Include long beds, loop variants, breathing, movement, bubbles, thrusters, UI/SFX, and any large unreferenced audio. Acceptance proof: listening/import/routing proof requirements stated. Fallback: no audio deletion from static absence.

24. Build the deletion-after-proof list. Only include rows that survived Tasks 6-23 with no source value, no vendor lock, no Unity readback concern, no visual/source/audio need, and no active blocker relevance. Acceptance proof: each row has proof gaps listed and status `DELETION_CANDIDATE_AFTER_PROOF_ONLY`. Fallback: any missing proof moves the row back to readback or QA.

25. Produce a concise final packet for the controller with totals per lane, top risk rows, required Unity readback list, QA list, deletion-after-proof list, rollback rules, `.meta` pairing rule, and unresolved blockers. Acceptance proof: final packet contains no readiness claims beyond its evidence class. Fallback: if totals cannot be produced, report `PENDING_STATIC_RECOUNT` and do not promote any deletion candidate.

Checkpoint 5 after Task 25:

- The result is a cleanup review packet, not a cleanup execution.
- Every current result is `STATIC / PENDING` unless a later Unity proof artifact is attached.
- Deletion candidates are blocked by explicit proof requirements.
- Generated textures and Gemini-watermark sources are preserved for QA unless later owner proof rejects them.

## Proof Packet Required From Future Owner

The future owner report must include:

- `Mandates followed`: exact mandate filenames used.
- `Input artifacts`: paths and timestamps for every audit file read.
- `Static command evidence`: commands used to count and classify rows.
- `Lane totals`: count by five review lanes.
- `Top risk rows`: high-size, first-party, active-product-face-adjacent, vendor, and scene rows.
- `Unity readback queue`: asset path, question, expected owner, status.
- `Visual/source QA queue`: asset path, visual/source question, expected comparison artifact, status.
- `Audio QA queue`: asset path, listening/import/routing question, status.
- `Deletion-after-proof queue`: asset path, missing proof, owner approval needed, rollback path.
- `Rollback rules`: exact restoration source and Git diff requirement for any later deletion task.
- `.meta` proof`: paired asset/meta handling rule and orphan scan command for later deletion owner.
- `Regression model`: CPU, GC, memory/VRAM, cadence, correctness, and failure modes.
- `Evidence boundary`: all current conclusions are static unless Unity/player/profiler/Frame Debugger/screenshot/audio proof is attached.

## Rollback Rules

This packet does not delete. If a later explicit deletion owner is assigned:

- Capture pre-delete `git status --short`.
- Record every asset path and matching `.meta` path before deletion.
- Delete asset and `.meta` together.
- Run an orphan `.meta` scan after deletion.
- Run the cheapest valid static reference scan after deletion.
- If Unity import is allowed for that later task, run Unity import/console readback after deletion.
- If any reference, import error, scene/prefab break, visual source loss, audio source loss, or owner objection appears, restore the asset and `.meta` from Git or recorded backup path.
- Never use broad directory wipes. Delete one reviewed row family at a time.

Suggested later orphan scan shape:

```powershell
Get-ChildItem -Path . -Recurse -Filter *.meta | Where-Object {
    $asset = $_.FullName.Substring(0, $_.FullName.Length - 5)
    -not (Test-Path -LiteralPath $asset)
}
```

## Rejection Gates

Reject the future-owner result if any of these occur:

- Claims deletion safety from static GUID absence alone.
- Deletes, moves, imports, or mutates assets during review.
- Deletes an asset without its paired `.meta`.
- Treats Gemini-watermark/source texture debt as automatic deletion proof.
- Treats vendor/third-party unreferenced rows as deletion-ready without owner approval.
- Claims visual readiness without accepted proof packet screenshots.
- Claims runtime/import/Addressables/audio readiness without Unity/readback evidence.
- Hides product-face visual blockers behind cleanup language.
- Writes reports into `Assets/`.
- Produces fake counts, fake hashes, fake timestamps, fake profiler numbers, or fake Unity status.

## Low / Middle / High / Ultra Consequences

- Low/Compact: cleanup review must protect source material that can preserve readable water, sky, terrain silhouette, HUD, audio cues, and route identity under tight memory. Do not delete source that may repair the currently rejected visual floor.
- Middle: quarantine should reduce confusion without removing candidates needed for shoreline, photic shallows, geology, Aegir/sky, and audio remediation.
- High: retained candidate sources can buy richer material detail, denser dressing, better normals/masks, and stronger audio beds after owner proof.
- Ultra: visual overkill sources remain candidate input until proof shows they are obsolete. Cleanup cannot remove high-tier source options just because compact route does not use them today.

## Regression Model

- CPU: static review only; no runtime CPU change.
- GC: no runtime code changed; no `0 B/frame` claim.
- Memory/VRAM: file size pressure is static only; no resident memory or texture streaming proof.
- Cadence: no runtime cadence changed.
- Correctness: reduces blind cleanup risk by forcing lane separation and proof gates; deletion correctness remains unproven until a later explicit deletion owner produces proof.
- Failure modes: stale CSV, missing Unity readback, hidden Addressables/runtime load route, vendor asset dependency, product-face source loss, paired `.meta` omission, false duplicate assumption, visual QA not performed.

Final status: `STATIC / PENDING VERIFICATION`.
