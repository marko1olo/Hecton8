# Rationale 1864

Date: 2026-06-04
Evidence class: STATIC_DOC / STATIC_SOURCE

## Decisions

- Excluded `Sky_System.prefab` and `Ocean_Crest.prefab` because task assigns them to 1865.
- Treated tool held and world pickup pairs as separate rows because exposure class differs: first-person versus pickup.
- Did not claim any replacement exists. Targeted static search found data/material assets and placeholder materials, but no accepted concrete replacement mesh/prefab path for the queued visuals.
- Kept `STRUCTURES.prefab` and `Buildings/Cube.prefab` as ambiguous/legacy rows, not hard production replacement claims, because static source cannot prove scene use.
- Required continuous `GlobalQualityWeight` consequences in every row. Low is not ugly mode; Ultra adds sensory detail only.

## Evidence Boundary

Static source proves prefab text, primitive GUID references, material GUID/path resolution, and absence of found candidate replacement paths in the targeted search. It does not prove Unity import, scene wiring, renderer appearance, gameplay exposure, profiler cost, build health, or acceptance.

## No-Edit Boundary

No source/prefab/asset/scene files were edited. Only owned 1864 status/log/rationale/report/matrix outputs were written.
