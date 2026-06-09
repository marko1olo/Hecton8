# Task Status Archive Boundary

Date: 2026-06-09
Status: STATIC POLICY
Owner: DOCS_ACTUALIZATION
Evidence class: STATIC_DOC / STATIC_FILESYSTEM

Purpose: define how `Docs/Tasks/Status_*.md` files are used during cleanup and source-truth routing.

## Classification

`Docs/Tasks` stores explicit-mode task status records, historical controller handoff notes, and a small number of legacy editor/runtime CSV inputs whose paths are still hardcoded in source. Status files are not route bibles, root doctrine, domain contracts, or current implementation proof.

## Active Non-Status Inputs

These files are source-consumed and must not be archived as task residue while the listed source paths still reference them:

| File | Current source owner | Runtime/editor role |
|---|---|---|
| `execution_priorities.csv` | `Assets/_Project/Scripts/Core/SystemDispatcher.cs` | Editor-only priority override parser; blank/comment-only preserves code-defined topology. |
| `validation_rules.csv` | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs` | Editor tuner input for validation frequency, AUP teleport tolerance, and strictness. |
| `shadow_culling_profiles.csv` | `Assets/_Project/Scripts/Graphics/Culling/AbyssalShadowCullingRuntime.cs` | Editor/tuner profile rules for abyssal shadow culling. |
| `telemetry_flags.csv` | `Assets/_Project/Scripts/Editor/BlackboxXRayViewer.cs` | Editor logging-mask override input for `GlobalTelemetryBus`; currently no active mask consumers were found. |
| `telemetry_hash_dictionary.csv` | `Assets/_Project/Scripts/Editor/BlackboxXRayViewer.cs` | Editor display dictionary for blackbox event hashes. |

If these CSVs are moved later, update source constants and tests in the same patch. Do not leave hardcoded paths pointing at missing files.

Use a status file only when one of these is true:

- the user gives the matching task ID or asks for that batch/status history;
- a current report, validator, or source document cites the exact status file;
- cleanup work needs exact-reference proof before moving stale task residue;
- a current explicit-mode controller/task workflow owns the status path.

Do not bulk-read `Status_*.md` files for ordinary work. Read root authority, route bibles, domain docs, current source, and fresh proof first.

## Cleanup Rules

Move stale task records to `Docs/DEPRECATED/` only after exact path/name/ID searches prove that no active source, validator, report chain, or current workflow depends on the file.

When a status file is moved:

- preserve the original filename under a dated deprecated bundle;
- update active citations that intentionally point to the archived record;
- do not rewrite the status content unless a broken link must be repaired after the move;
- do not convert historical task prose into active doctrine.

If a task record contains a durable fact that still matters, promote only that fact into the owning active contract after source/proof review.

## Active Use Boundary

Status files can explain why a historical decision was made. They do not prove that code still compiles, Unity still imports, scenes are wired, runtime behavior works, or a player-visible feature is ready.

Current readiness claims require fresh proof artifacts under the relevant evidence gate.
