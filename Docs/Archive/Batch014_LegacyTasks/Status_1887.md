# Status 1887

Agent: 1887
Task: PRODUCT_FACE_LEGACY_REFERENCE_QUARANTINE_DECISION_PACKET
Mode: REPORT_ONLY_STATIC_REFERENCE_AUDIT
Evidence class: STATIC_SOURCE / STATIC_DOC

## State

`STATIC VERIFIED` for report-only scan outputs. Runtime, Unity import, PlayMode, visual quality, profiler, Frame Debugger, and build state remain `PENDING VERIFICATION`.

## Outputs

- `Docs/Reports/Batch18/1887_PRODUCT_FACE_LEGACY_REFERENCE_QUARANTINE_DECISION_PACKET.md`
- `Docs/Reports/Batch18/1887_PRODUCT_FACE_LEGACY_REFERENCE_MATRIX.csv`
- `Docs/AgentLogs/Rationale_1887.md`
- `Docs/AgentLogs/LOG_1887.md`

## Decisions

- `Item_Titanium.prefab`: `QUARANTINE_CANDIDATE_PENDING_REFERENCE_PROOF`; if retained, canonical `TitaniumScrap` mesh/material/data truth only.
- `STRUCTURES.prefab`: `QUARANTINE_CANDIDATE_PENDING_REFERENCE_PROOF`; must not retain primitive child via `Item_Titanium`.
- `Buildings/Cube.prefab`: `DELETE_FORBIDDEN_WITHOUT_UNITY_OWNER`; GUID reference exists in `Assets/MapMagic/Map_Graph/Old tries/Terrain.asset`.

## Highest Risks

- Package/default material GUID `31321ba15b8f8eb4c954353edc038b1d` is on `Item_Titanium`, `STRUCTURES`, `Tool_Propulsion_Held`, player, and transport product-face roots.
- `Tool_Propulsion_Held` resolves to package-cache URP `Lit.mat` and must be replaced by project-owned material source before acceptance.
- `Item_Titanium` has editor/bootstrap/validator references and a `ScannableTarget`; static report cannot approve quarantine.
- `Buildings/Cube` has an asset GUID reference in a MapMagic graph; static report cannot classify it obsolete.

## Verification

- `git diff --check -- Docs/Reports/Batch18/1887_PRODUCT_FACE_LEGACY_REFERENCE_QUARANTINE_DECISION_PACKET.md Docs/Reports/Batch18/1887_PRODUCT_FACE_LEGACY_REFERENCE_MATRIX.csv Docs/Tasks/Status_1887.md Docs/AgentLogs/Rationale_1887.md Docs/AgentLogs/LOG_1887.md` -> PASS, no output.
- `Import-Csv Docs/Reports/Batch18/1887_PRODUCT_FACE_LEGACY_REFERENCE_MATRIX.csv | Measure-Object` -> PASS, `Count: 12`.
- Static term cross-check for `Item_Titanium`, `STRUCTURES`, `Buildings/Cube`, `31321ba15b8f8eb4c954353edc038b1d`, `Lit.mat`, `Data_TitaniumScrap`, `Tool_Propulsion_Held`, and `QUARANTINE` across owned report/CSV/status/rationale/log -> PASS, hits present.
