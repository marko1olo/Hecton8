# LOG_SHINOBU_66

## 2026-05-19 - Modding SDK Documentation Fix

What was wrong:
- Root modding docs still described legacy managed mod callbacks, projected events, resource proxy, content overlays, and old command lanes as if they were current modder workflow.
- That contradicted the SHINOBU_66 quarantine result: public UGC runtime ingress is fixed 64-byte `FutureCommandEnvelope` only.
- The existing static validator expected old sequential `ModCommand` layout while source had moved the dormant legacy DTO to explicit 64-byte overlay layout.

What was done:
- Added `Docs/Modding/SDK_Authoring_Interface_Plan.md` as the canonical human-facing SDK/workbench/CLI/graph/package plan.
- Updated the modding index, API spec, sandbox quarantine doc, runtime playbook, change checklist, payload matrix, command matrix, API surface audit, event audit, signal audit, loader/save audit, resource/content audit, sample mod spec, future reservation doc, schema, and static validator.
- Marked legacy managed callbacks and content/resource ingress as historical/source-audit context while envelope-only mode is active.
- Added current `FutureCommandEnvelope` payload layout to schema/docs and updated `ModCommand` documentation to explicit 64-byte source reality.

Cinematic Cheats used:
- Human modder ergonomics are moved into offline/editor SDK tooling rather than runtime code execution.
- Command graphs compile into bounded envelope streams; unsupported future seams simulate as rejection/DevNull instead of running gameplay code.

Exact Microseconds saved:
- Runtime measured proof absent. This is a documentation/static-validator update.
- Expected runtime protection remains from prior quarantine architecture: no managed callback dispatch, no legacy command allocator boot, no loose content ingress, no direct Unity object exposure.

Verification:
- `Get-Content -Raw Docs/Modding/Signal_Schema.json | ConvertFrom-Json` passed.
- `git diff --check -- Docs/Modding` passed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` passed.
- ASCII scan on touched modding docs returned no non-ASCII matches.

## 2026-05-19 - SDK Product Blueprint Expansion

What was wrong:
- The first SDK doc defined the architecture but still read like an engineering contract. It did not fully specify the product surfaces modders and SDK developers need: screens, CLI behavior, package layout, graph compiler UX, Workshop moderation, support states, MVP scope, and rejection language.

What was done:
- Added `Docs/Modding/SDK_Product_Blueprint.md`.
- Linked it from `Docs/Modding/README.md`, `SDK_Authoring_Interface_Plan.md`, `Change_Control_Checklist.md`, and `Runtime_Verification_Playbook.md`.
- Kept the same runtime boundary: SDK tooling can be friendly and managed, but exported runtime data is package metadata, fixed binary tables, approved asset manifests, and 64-byte `FutureCommandEnvelope` streams only.

Cinematic Cheats used:
- Replaced the "user script in game" fantasy with offline graph compilation and simulation. The modder gets a usable creation suite; the runtime gets bounded packets.

Exact Microseconds saved:
- Runtime measured proof absent. This is a documentation/product blueprint update.
- Expected protection remains architectural: no runtime C# mod callback lane, no Harmony patches, no loose content ingestion, no direct engine object exposure.

Verification:
- `git diff --check -- Docs/Modding` passed.
- `Get-Content -Raw Docs/Modding/Signal_Schema.json | ConvertFrom-Json` passed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` passed.
- ASCII scan on the newly linked SDK/root docs returned no non-ASCII matches.
