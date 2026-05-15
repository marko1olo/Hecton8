# UX_ENGINEER Rationale

Prompt ID: HARDWARE_ADAPTIVE_UI_BAKER
Domain: PRESENTATION & UX
Status: PENDING VERIFICATION

## Pre-Code Mandate Selection
Problem: UI scaler task touches TMP readability, diegetic VR layout, icon offline baking, contrast tiers, and evidence claims.
Solution: Loaded only relevant mandates for zero-GC text, diegetic UI, foveated/VR scaling, stencil rejection, performance budgets, visual-fake-first, and evidence reporting.
Rejected Alternatives: Reading all registry mandates would add noise and risk cross-domain contamination; using archived UI logs is blocked by batch hygiene unless explicitly requested.
Scalability potential: Low uses static high-contrast UI and cheap SDF dilation. Middle uses stable SDF tuning and restrained panel shift. High/Ultra can spend saved cost on blur/glitch/chromatic treatment with tier gates.
Hardware Impact: Target is static/offline configuration and Python baking; expected runtime hot-path cost is 0 us unless an existing UI scaler owner requires a cold-init-only patch. MX350 gains come from avoiding extra texture samples and runtime render-scale churn.

## Evidence Classes
- Batch prompt extraction: STATIC_DOC.
- Mandate reads: STATIC_DOC.
- Existing code scan: STATIC_SOURCE, pending full owner inspection.

## Decision - TMP SDF Matrix In Existing Controller
Problem: Prompt requires dynamic TMP-SDF weighting, but adding a second scaler would create duplicate ownership and likely per-frame drift.
Solution: Extended `WorldSpaceTMPSharpnessController` with `_WeightNormal`, `_WeightBold`, `_FaceDilate`, and `_OutlineSoftness` resolution buckets. It rides the existing late-frame throttled sharpness cadence.
Rejected Alternatives: Runtime font-asset swaps were rejected because they can trigger atlas churn and layout rebuilds. Per-frame material updates were rejected because the controller already has a 0.1s material write cadence.
Scalability potential: Low/TOASTER thickens SDF ink and reduces softness. Middle/High reduce extra dilate. Ultra thins glyphs and allows visual overkill through post, not widget shader samples.
Hardware Impact: Static estimate 6-12 us saved per SDF profile update versus font swap/material churn on i3/MX350. Evidence class is STATIC_SOURCE; profiler proof absent.

## Decision - FOV Layout As Contract, Not New Dependency
Problem: Quest 2 and Quest 3 need different diegetic button placement, but direct device branching would create a hardware dependency in UI layout.
Solution: Defined bake presets while keeping runtime authority as `projectionCamera.fieldOfView` with 2 degree / 2 second hysteresis and VISUAL_SYNC ownership.
Rejected Alternatives: Hard-coded OpenXR device checks and direct HMD SDK dependency were rejected; they are brittle and cross the UX/Core boundary.
Scalability potential: Low keeps controls inward and large. High/Ultra can push edge clusters outward and spend optical space on denser visor post.
Hardware Impact: Static estimate 20-60 us saved on FOV changes by avoiding layout rebuild paths. Evidence class is STATIC_DOC / STATIC_SOURCE.

## Decision - Two-Sample Shader Cap
Problem: Several owned UI shaders exceeded the two-sample target: curved HUD chroma, acoustic radar neighbor taps, tool screen multi-texture combine, and diegetic panel dual texture blend.
Solution: Replaced multi-sample effects with deterministic math fakes or single source textures. Scene depth counts as a texture sample in the audit.
Rejected Alternatives: Keeping chromatic aberration as extra widget samples was rejected; GOD_MODE can use a post pass. Keeping neighbor samples for radar smoothing was rejected; angular math widening preserves belief cheaper.
Scalability potential: Low gets hard, readable UI with one or two samples. Ultra spends saved samples on global HUD RT/post passes instead of every widget.
Hardware Impact: Static estimate 0.05-0.20 ms GPU avoided across HUD-heavy frames on MX350. Evidence class is STATIC_SOURCE + PYTHON_STATIC_AUDIT; Frame Debugger proof absent.

## Decision - Offline Icon Baker
Problem: Prompt requires 32/128/512 icon outputs and pixel snapping.
Solution: Added `Tools/IconBaker.py` with transparent trim, square centering, fixed-size output, and 32px alpha snapping.
Rejected Alternatives: Runtime scale variants were rejected because bilinear minification makes critical icons unstable and wastes bandwidth.
Scalability potential: Low uses crisp 32/128 assets. Ultra can use 512 assets without changing runtime logic.
Hardware Impact: Static estimate 5-25 us per icon draw avoided by preventing runtime resizing/import ambiguity. Evidence class is PYTHON_SELF_TEST.

## Decision - Blur Readability Test
Problem: "O2 LOW" readability under poor vision/low resolution needed objective proof, not a visual claim.
Solution: Added `Tools/UX/ui_readability_test.py`; it renders text, applies blur/downsample degradation, and checks contrast, template correlation, and ink survival.
Rejected Alternatives: Manual screenshot inspection was rejected because it is not repeatable and cannot fail CI.
Scalability potential: The same test can harden future warning strings and localization glyphs.
Hardware Impact: Offline QA tool only; runtime frame impact is 0 us. Evidence class is PYTHON_REPORT.

## Decision - Industrial Brutalism Rationale
Problem: The prompt requires a style rationale that still obeys performance and readability constraints.
Solution: Defined the UI as stamped industrial instrumentation: hard edges, black-backed emergency panels, short labels, thick warning glyphs, and scanline/noir effects as math fakes.
Rejected Alternatives: Soft consumer-glass UI was rejected because it lowers contrast and tends to hide information behind blur. A balanced middle-ground profile was rejected because the scalability pillar demands a toaster path and visual-overkill path.
Scalability potential: Low/Middle prioritize high contrast and legibility. High/Ultra add blur, dirt, chroma, and pressure damage through HUD RT/post, not per-element texture samples.
Hardware Impact: Low-end i3/MX350 avoids extra widget samples and layout rebuilds. High-end hardware spends saved budget on global presentation passes. Exact savings remain PENDING PROFILER.

## Verification Commands
- `python -m py_compile Tools/IconBaker.py Tools/UX/ui_readability_test.py Tools/UX/ui_shader_sample_audit.py` -> PASS.
- `python Tools/UX/ui_readability_test.py --write-report` -> PASS; report `Docs/AgentLogs/UI_Readability_UX_ENGINEER.json`.
- `python Tools/UX/ui_shader_sample_audit.py --write-report` -> PASS; report `Docs/AgentLogs/UI_ShaderSampleAudit_UX_ENGINEER.json`.
- `python Tools/IconBaker.py --self-test --output Docs/AgentLogs/IconBaker_UX_ENGINEER_SelfTest --manifest Docs/AgentLogs/IconBaker_UX_ENGINEER_SelfTest/IconBakeManifest.json` -> PASS.
- `python -m unittest Tools.UX.test_hardware_adaptive_ui -v` -> PASS; 5 tests cover spec identity, C# matrix parity, readability, shader sample audit, and IconBaker sizes/alpha snapping.
- `python -m json.tool` on scaler spec/reports/manifest -> PASS.

## Residual Risk
Problem: Unity import, shader compile, Frame Debugger, GCMonitor, and visual proof were not available from this shell pass.
Solution: Mark runtime/visual claims as PENDING UNITY VERIFICATION.
Rejected Alternatives: Claiming Unity verified from Python/static scans is forbidden by QA_Evidence_Text_Filter_Audit.
Scalability potential: The artifacts are ready for Unity validation on MX350 and high-tier profiles.
Hardware Impact: No measured hardware data exists in this pass.

## Polish Mandate Check
Problem: Status reached 100%, but `Docs/Tasks/CURRENT_BATCH.md` contains no `<POLISH_MANDATE>` tag.
Solution: Treated the tag as absent and ran local anti-bloat checks anyway: Python compile, JSON validation, shader sample audit, hot-path text scan, and diff whitespace check.
Rejected Alternatives: Inventing a polish mandate was rejected because batch protocol requires reading the actual tag.
Scalability potential: Final artifacts stay bounded to UI/scaler/tooling and do not introduce new cross-domain dependencies.
Hardware Impact: No additional runtime systems were added during polish.

## Continuation - Regression Harness
Problem: The implementation was complete, but future edits could desynchronize the JSON matrix and C# runtime bucket values.
Solution: Added `Tools/UX/test_hardware_adaptive_ui.py` to fail if the spec, C# runtime matrix, readability simulation, shader sample cap, or IconBaker output drift.
Rejected Alternatives: Leaving only ad hoc command output was rejected because it cannot defend the work in later batch churn.
Scalability potential: The harness protects Low/Middle/High/Ultra bucket intent from silent edits.
Hardware Impact: Offline test only; runtime impact is 0 us.

## Unity Verification Boundary
Problem: Project requires Unity `6000.4.1f1`, but no local Unity executable was found via normal install paths or command lookup during this pass.
Solution: Recorded Unity import/Console/PlayMode/Frame Debugger as PENDING VERIFICATION.
Rejected Alternatives: Claiming Unity compile from Python/unittest evidence was rejected.
Scalability potential: Once Unity is available, run scene import and Frame Debugger on MX350 and high-tier profiles.
Hardware Impact: No hardware runtime data exists.

## Final Hardening Rerun
Problem: Python verification can create `__pycache__` churn and stale bytecode artifacts.
Solution: Removed generated `IconBaker.cpython-314.pyc` and reran final checks with `PYTHONDONTWRITEBYTECODE=1`.
Rejected Alternatives: Leaving tool cache artifacts in the active workset was rejected as avoidable noise.
Scalability potential: Source-only tooling is cleaner for other agents and CI.
Hardware Impact: Offline hygiene only; runtime impact 0 us.
