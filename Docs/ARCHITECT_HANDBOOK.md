# Architect Handbook

Date: 2026-05-26
Status: GENERATED CONTRACT STUB
Owner: DOCS_ACTUALIZATION
Evidence class: STATIC_DOC / GENERATED_SOURCE_INDEX

Purpose: stable root path for tools that generate or validate the architecture constants handbook.

Full pre-distillation snapshot: `Docs/DEPRECATED/Root_Generated_Snapshots_2026-05-26/ARCHITECT_HANDBOOK.md`.

## Authority

- C# source contracts under `Assets/_Project` own constants, DTO layouts, and runtime truth.
- This file is not source truth, compile proof, Unity import proof, runtime proof, profiler proof, or memory proof.
- Regenerate the full handbook with `Tools/ContractAuthority/Generate-ArchitectHandbook.ps1` when contract source changes.
- Validate the generated contract surface with `Tools/ContractAuthority/Test-ContractAuthority.ps1`.

## Contract Rules

- `GlobalQualityWeight` is continuous. Named quality tiers are documentation anchors only, not binary runtime switches.
- Read accessors stay pure: no allocation, scene search, publication, global mutation, sync, or hidden job completion.
- Native ownership must be explicit: owner route, allocator, disposal, lifetime, and proof artifact.
- Generated handbook content belongs in reports or deprecated snapshots until a specific fact is promoted into an active contract.
