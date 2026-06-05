# Rationale 3102 - Proof Harness 1475 Owner

## Mandates Followed

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## Decisions

- Do not implement while Unity/dotnet/compiler/import processes are active. Current gate is blocked by Unity `11620`, dotnet `15340`, Unity.ILPP.Runner `13512`, UnityAutoQuitter `13852`, UnityShaderCompiler `9532`.
- Do not extend `H8VisualProofCapture1912.cs`. Static source shows `CaptureRoot` points to `Docs/Screenshots/MCP`, quarantine can disable renderers, and quarantine marks/saves the production scene.
- Treat `Tools/ProofGate/validate_proof_packet.py` as current static schema authority for packet validation. It already enforces manifest fields, six canonical production screenshots, `qNNN` labels, depth/UI/route predicates, checksum/freshness rules, dirty-token log scanning, raw PNG rejection, and strict unknown-file rejection.
- Future implementation must be new-file only: runtime DTO/contracts under `Assets/_Project/Scripts/Proof/Capture/`; editor harness under `Assets/_Project/Scripts/Editor/Proof/`.
- Future harness must never call `SaveScene`, `MarkSceneDirty`, renderer-disable quarantine, `AssetDatabase.Refresh`, or write diagnostics under `Assets`.

## Regression Model

- CPU/GC/memory/cadence: no runtime or editor code changed in this pass. No runtime claim.
- Correctness: blocked implementation avoids contaminating active Unity import/compile and avoids scene mutation while another owner may have dirty state.
- Failure mode if ignored: imported half-harness, dirty log window, scene save contamination, or fake raw-PNG acceptance.

Proof state: `STATIC VERIFIED` for source/schema inspection only. Runtime/editor capture remains `PENDING VERIFICATION`.
