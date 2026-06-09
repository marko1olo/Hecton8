# HECTON-8 Local Task Packets

Date: 2026-06-09
Status: STATIC BOUNDARY
Owner: ORCHESTRATION_HYGIENE
Evidence class: STATIC_DOC / STATIC_FILESYSTEM

Purpose: define the boundary for local standalone agent task packets.

`taskslocal/` stores explicit standalone dispatch packets and historical batch provenance. It is not a root authority, not a domain bible, not current implementation truth, and not a general context folder.

## Active Use

Read a task packet only when one of these is true:

- the user explicitly assigns that packet or batch;
- a current `Docs/Tasks/Status_<ID>.md`, report, validator, or source file cites the exact path;
- you are creating, judging, or materially rewriting a standalone batch under `HECTON8_ORCHESTRATOR.md`;
- you are doing archival hygiene and need exact-reference proof before moving a batch.

New or materially rewritten serious batches must pass:

`python -B Tools/Docs/TestTaskLocalLaneContracts.py taskslocal/<batch_name> --strict`

Historical batches may be inspected with `--allow-legacy`. Do not make old task folders a standing failure gate unless they are reissued.

## Not Source Of Truth

Task packets are instructions or handoff contracts. They do not prove:

- Unity import, Console, Play Mode, profiler, GC, build, save/load, or scene readiness;
- current architecture state after later source changes;
- asset acceptance after later import, material, or validator changes;
- that a referenced status/log/report still exists or is current.

For current truth, use root authority, route bibles, current source, active assets, and fresh proof artifacts.

## Cleanup Rules

Do not bulk-delete or bulk-archive `taskslocal/`.

Before moving a batch to `Docs/DEPRECATED/`, verify all of the following:

- no active source, validator, report, status, or active docs cite the exact task path, batch name, or packet filename;
- no active tool uses files in the batch as static input;
- no current controller/orchestrator wave still depends on the batch;
- the whole batch directory is moved together so cross-packet references remain readable as historical provenance.

If only a status/report is stale, archive that status/report first. Do not move the task packet just because the execution note is stale.

## Reading Discipline

Use exact IDs and filenames. Do not bulk-read old batches as a substitute for `AGENTS.md`, `PROJECT_BIBLES.md`, route bibles, or source inspection.
