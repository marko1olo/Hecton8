# 1895 ProductFace Static Route Audit Tool

Agent: 1895
Evidence class: STATIC_SOURCE / STATIC_DOC / PYTHON_COMPILE
Unity/build/import/PlayMode/profiler/screenshots/DataMonolith: NOT RUN

## Scope

Implemented `Tools/ProductFaceStaticRouteAudit.py`, a read-only Python audit command for current ProductFace static route contracts.

The tool checks:

- required ProductFace source and validator file presence;
- current generated route roots for Tools, Resources, Transport, and PlayerSuit;
- stale generated route roots in ProductFace source files and the 1879 relink contract files;
- forbidden ProductFace source tokens: `CreatePrimitive`, `GameObject.CreatePrimitive`, `float.IsFinite`, `double.IsFinite`;
- ProductFace report proof-boundary language, including `PENDING UNITY`, `PENDING VERIFICATION`, or `NOT RUN`;
- 1891 warning against generic `ai_texture_prefab_bindings.csv` product-face binding;
- 1889 environment exclusion report presence and required `Crest`, `terrain`, `storm`, `noir`, `depth` terms;
- scoped text file size limits so huge binary files are not inspected.

The tool does not write reports automatically and does not inspect the project broadly. It only scans curated ProductFace source/report/CSV paths.

## Authorities And Mandates

Read:

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- required Batch18 ProductFace reports 1874, 1875, 1876, 1877, 1878, 1879, 1888, 1889, 1890, 1891
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/TOOL_Designer_Facades_CSV_Binary_Bridge.txt`

Required mandate absent:

- `.agents-skills/PERF_Runtime_CPU_GC_ZeroAlloc.txt`

## First Run Output

Initial implementation run exposed a false positive in the `In-game result:` regex: reports that correctly said `PENDING VERIFICATION` were flagged because optional whitespace backtracked before the pending token.

First flawed output:

```text
ProductFace static route audit
ERROR: 3
WARNING: 0
INFO: 0
Findings:
[ERROR] UNSUPPORTED_RUNTIME_ACCEPTANCE_CLAIM Docs/Reports/Batch18/1874_TOOL_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md:163 - Potential runtime/visual proof upgrade without accepted evidence boundary: In-game result: PENDING VERIFICATION. Unity execution was forbidden.
[ERROR] UNSUPPORTED_RUNTIME_ACCEPTANCE_CLAIM Docs/Reports/Batch18/1875_RESOURCE_PICKUP_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md:131 - Potential runtime/visual proof upgrade without accepted evidence boundary: In-game result: PENDING VERIFICATION. Unity execution was forbidden.
[ERROR] UNSUPPORTED_RUNTIME_ACCEPTANCE_CLAIM Docs/Reports/Batch18/1888_PRODUCT_FACE_TEXTURE_CHANNEL_MANIFEST_AND_SHADER_AUDIT.md:186 - Potential runtime/visual proof upgrade without accepted evidence boundary: In-game result: PENDING VERIFICATION. Unity, screenshots, import, PlayMode, Frame Debugger, profiler, and DataMonolith were not run by task order.
```

Correction made: `In-game result:` is now parsed line-by-line instead of through a backtracking regex.

## Final Verification Commands

```powershell
python -m py_compile Tools/ProductFaceStaticRouteAudit.py
```

Output:

```text
```

Exit code: 0.

```powershell
python Tools/ProductFaceStaticRouteAudit.py --root .
```

Output:

```text
ProductFace static route audit
ERROR: 0
WARNING: 0
INFO: 0
No findings.
```

Exit code: 0.

```powershell
python Tools/ProductFaceStaticRouteAudit.py --root . --json
```

Output:

```json
{
  "counts": {
    "ERROR": 0,
    "INFO": 0,
    "WARNING": 0
  },
  "findings": [],
  "root": "C:\\hades\\Hecton8",
  "tool": "ProductFaceStaticRouteAudit"
}
```

Exit code: 0.

```powershell
python Tools/ProductFaceStaticRouteAudit.py --root . --fail-on-error
```

Output:

```text
ProductFace static route audit
ERROR: 0
WARNING: 0
INFO: 0
No findings.
exit=0
```

Exit behavior: default exit is 0. With `--fail-on-error`, current exit is 0 because current error count is 0. If future errors exist, `--fail-on-error` returns non-zero.

```powershell
git diff --check -- Tools/ProductFaceStaticRouteAudit.py Docs/Reports/Batch18/1895_PRODUCT_FACE_STATIC_ROUTE_AUDIT_TOOL.md Docs/Tasks/Status_1895.md Docs/AgentLogs/Rationale_1895.md Docs/AgentLogs/LOG_1895.md
```

Output:

```text
```

Exit code: 0.

## Current Findings

Final corrected tool result:

- Errors: 0
- Warnings: 0
- Info: 0

No current static ProductFace route findings were emitted by the corrected tool.

## Why This Does Not Replace Unity Validators

This tool proves text/source/report consistency only. It cannot prove Unity import, C# assembly compilation inside Unity, menu execution, prefab relink safety, material/texture asset resolution through `AssetDatabase`, scene wiring, active renderer state, screenshots, Frame Debugger state, profiler cost, GC allocation, or player-visible visual quality.

Future Unity owner still must run:

- `Hecton8/Validation/Product-Face Prefab Quality Gate`
- `Hecton8/Validation/Sky-Ocean Source Primitive Gate`
- `Hecton8/Validation/Product-Face Material Texture Gate`
- `python Tools/GeneratedAssetProductionAudit.py --root . --fail-on-error`
- route screenshots and profiler/Frame Debugger/GC proof when visual/runtime acceptance is claimed

## Low / Middle / High / Ultra Consequences

Low: avoids wasting a constrained Unity slot on stale generated paths, missing validators, or forbidden ProductFace source tokens. It does not permit flat or placeholder product-face art.

Middle: keeps current source/report contracts aligned before material, texture, and relink owners spend editor time.

High: catches generic AI texture binding and channel-proof drift before richer ProductFace materials are promoted.

Ultra: supports stricter forensic/static gates, but does not change gameplay truth, material role semantics, prefab authority, save identity, DTO layout, or runtime visual acceptance.

## Result

What was wrong: ProductFace route work lacked a small static command to catch stale route strings, missing validators, generic AI binding drift, and proof-boundary regressions before a Unity slot.

What I did: added the read-only Python audit tool and documented corrected verification output.

In-game result: PENDING VERIFICATION. Unity was not run by task order.

What was verified: Python syntax compile, static audit text output, static audit JSON output, `--fail-on-error` current zero-error exit behavior, and owned-file whitespace diff check.
