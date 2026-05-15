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

## Decision - Runbook Boundary
Problem: The local Python/static validation is reproducible, but Unity import, GCMonitor, Frame Debugger, and RenderDoc evidence cannot be faked from a shell-only session.
Solution: Added `Docs/Design/HardwareAdaptiveUIScaler_Runbook.md` with the aggregate validation command, expected artifacts, Unity-only gates, and failure handling. This preserves the completed static work while preventing a false runtime completion claim.
Rejected Alternatives: Updating generated JSON reports by hand was rejected. Touching validated scaler code or shader artifacts without a new failing test was rejected.
Scalability potential: Low/Middle/High/Ultra behavior remains governed by the existing JSON and C# matrix. The runbook only hardens operator procedure.
Hardware Impact: Runtime impact is 0 us. The gain is evidence discipline: no MX350 performance claim is promoted without Unity-side GC and Frame Debugger data.

## Decision - Unity Evidence Template
Problem: The remaining gap is not design or Python validation; it is Unity runtime evidence. Without a fixed schema, an integrator could attach partial screenshots and still leave GC, shader samples, or FOV hysteresis unproven.
Solution: Added `Docs/Design/HardwareAdaptiveUIScaler_UnityVerificationTemplate.json` with required PASS checks for import, GCMonitor, Frame Debugger, Quest 2/3 FOV layout, O2 LOW readability capture, MX350 low tier, and GOD_MODE capture.
Rejected Alternatives: Marking the static work as runtime-complete was rejected. Adding editor automation without an available Unity executable or MCP bridge was rejected because it would be dead code in this environment.
Scalability potential: The template forces both toaster and top-tier capture evidence: TOASTER must prove solid high-contrast readability, GOD_MODE must prove visual overkill through gated post treatment without violating per-widget sample budgets.
Hardware Impact: Runtime impact is 0 us. Evidence impact is high: low-end i3/MX350 claims stay blocked until GCMonitor and Frame Debugger data exist.

## Decision - Unity Template Static Audit
Problem: A template alone can rot or be edited into a fake pass state. The project needs a local static check that rejects accidental runtime-complete status before Unity evidence exists.
Solution: Added `Tools/UX/validate_unity_verification_template.py`. It validates required check IDs, PENDING statuses, empty evidence paths in the template, owner/prompt identity, and acceptance-rule wording.
Rejected Alternatives: Folding this into Unity runtime verification was rejected because the purpose is different: this is a pre-Unity guard. Trusting manual review was rejected because the batch requires evidence-based gates.
Scalability potential: The audit preserves the Low/MX350 and GOD_MODE proof requirements by keeping both capture checks mandatory.
Hardware Impact: Runtime impact is 0 us. The script is offline tooling. It prevents promoting low-end readability or high-end overkill claims without evidence.

## Decision - Runner Timeout Boundary
Problem: The validator printed a PASS line, but the shell wrapper timed out and did not return a clean exit code. Treating that as full command verification would overstate evidence.
Solution: Recorded the condition as TOOL_RUNNER_TIMEOUT and corrected the runbook so the aggregate runner and template audit are distinct commands until the aggregate runner is explicitly extended.
Rejected Alternatives: Claiming a clean validator run was rejected because the wrapper did not return exit code 0. Hiding the new audit inside runbook text without tooling was rejected; the validator file exists and can be rerun.
Scalability potential: No runtime change. The guard keeps both low-end and GOD_MODE verification requirements mandatory.
Hardware Impact: Runtime impact is 0 us. Evidence impact: prevents accidental promotion of runtime claims from partial local output.

## Decision - Filled Unity Report Audit
Problem: The template protects the blank form, but the actual runtime evidence report also needs a validator. Otherwise a report could be marked PASS with missing screenshots, GC logs, or Frame Debugger captures.
Solution: Added `Docs/AgentLogs/UnityVerification_UX_ENGINEER.json` as a pending report and `Tools/UX/validate_unity_verification_report.py` to reject runtime PASS unless every required check is PASS with a non-empty evidence path.
Rejected Alternatives: Leaving Unity verification as a free-form markdown note was rejected because the batch demands objective evidence. Embedding fake paths was rejected because no editor proof exists in this session.
Scalability potential: The report audit keeps TOASTER/MX350 and GOD_MODE captures mandatory, forcing proof for both ends of the scalability pillar.
Hardware Impact: Runtime impact is 0 us. Evidence impact is high: low-end readability and high-end visual-overkill claims cannot pass without concrete files.

## Decision - Report Audit Execution Timeout
Problem: `py_compile` and report-audit execution were attempted after adding the filled-report validator, but the shell wrapper timed out without useful output.
Solution: Kept the validator and pending report on disk, but recorded execution as PENDING instead of passing it. The runbook still lists the exact command for a stable shell rerun.
Rejected Alternatives: Claiming validation success from a timed-out wrapper was rejected. Removing the validator because the runner is unstable was rejected; the file is deterministic offline tooling and improves the handoff.
Scalability potential: No runtime change. The report validator continues to require both MX350 and GOD_MODE evidence before runtime PASS.
Hardware Impact: Runtime impact is 0 us. Evidence risk is explicit: script syntax/runtime remains unproven until command execution returns cleanly.

## Decision - Aggregate Validation V2
Problem: The standard aggregate validator did not yet include the new Unity-template and Unity-report evidence gates. That allowed someone to run the old aggregate command and miss the latest validation layer.
Solution: Rebuilt `Tools/UX/run_hardware_adaptive_ui_validation.py` as schema v2. It now runs readability, shader sample audit, IconBaker self-test, Unity template audit, Unity report audit, and the unit harness, then hashes the owned source, docs, reports, and tooling artifacts.
Rejected Alternatives: Leaving the new validators as only direct commands was rejected because the batch needs one reproducible local entry point. Changing runtime UI code was rejected because the remaining gap is evidence, not behavior.
Scalability potential: No runtime behavior change. The aggregate now enforces that both TOASTER/MX350 and GOD_MODE evidence gates stay wired into the validation path.
Hardware Impact: Runtime impact is 0 us. Offline validation time increases, but it protects low-end and high-end claims from stale reports.

## Decision - Aggregate V2 Execution Timeout
Problem: After rebuilding the aggregate runner, both Python compile and aggregate execution attempts timed out with no output from the shell wrapper.
Solution: Recorded v2 execution as PENDING. The runner remains on disk for stable-shell execution, but no fresh PASS report is claimed.
Rejected Alternatives: Assuming the v2 runner passes because the previous v1 report passed was rejected; v2 has new command coverage and must earn its own clean exit.
Scalability potential: No runtime change. The validation path still encodes low-end/high-end gates for future execution.
Hardware Impact: Runtime impact is 0 us. Evidence confidence for v2 is pending until command execution returns normally.

## Decision - Explicit Blocker Record
Problem: Repeated user continuation requests can blur the difference between unfinished engineering work and blocked runtime verification. Without a single blocker record, the next integrator could miss which evidence is absent.
Solution: Added `Docs/AgentLogs/Blocker_UX_ENGINEER.md` with environment blockers, completed static scope, stable-shell commands, Unity closure requirements, and the exact runtime PASS boundary.
Rejected Alternatives: Continuing to add runtime code while status/rationale reads and Python runs time out was rejected. Declaring completion from static reports was rejected.
Scalability potential: The blocker record keeps both low-end and GOD_MODE closure evidence mandatory.
Hardware Impact: Runtime impact is 0 us. It prevents false i3/MX350 and top-tier claims until actual Unity evidence exists.

## Decision - Unity Gate Unit Tests
Problem: The Unity evidence validators existed, but their failure behavior needed test coverage so a future edit cannot silently allow fake runtime PASS.
Solution: Added `Tools/UX/test_unity_verification_gates.py` and wired it into aggregate validation v2. It tests the locked template, fake template PASS rejection, pending report acceptance, missing-evidence PASS rejection, and complete synthetic PASS acceptance.
Rejected Alternatives: Trusting the validators because their code is short was rejected. Runtime code changes were rejected because the current gap is evidence gate hardening.
Scalability potential: The tests preserve both MX350 and GOD_MODE evidence requirements through the shared required-check set.
Hardware Impact: Runtime impact is 0 us. Offline validation coverage increases.

## Decision - Aggregate V2 Static Pass
Problem: Previous aggregate v2 execution was pending because the shell wrapper timed out. That left the new validation path unproven.
Solution: Reran the new evidence-gate tests and aggregate v2 command after the runner recovered. `py_compile` returned clean, `Tools.UX.test_unity_verification_gates` passed 5/5, and `python Tools/UX/run_hardware_adaptive_ui_validation.py` returned PASS.
Rejected Alternatives: Stopping at earlier v1 validation was rejected because v2 added Unity template/report gates and artifact hashes.
Scalability potential: Static validation now enforces TOASTER/MX350 and GOD_MODE evidence gates before runtime promotion.
Hardware Impact: Runtime impact is 0 us. Static validation confidence improved; Unity profiler and frame debugger evidence still required for hardware claims.

## Decision - Unity Compile Log Audit
Problem: Runtime verification needs a deterministic way to reject Unity import logs with C# compile errors, shader errors, batchmode failures, or refresh/import errors.
Solution: Added `Tools/UX/unity_compile_log_audit.py` plus `Tools/UX/run_unity_import_check.ps1`. The Python auditor is covered by unit tests with clean, C# error, and shader error log text.
Rejected Alternatives: Manual log inspection was rejected because it can miss errors under batch churn. Claiming Unity import from missing editor paths was rejected.
Scalability potential: The log audit is independent of tier, but it protects all TOASTER/MX350 and GOD_MODE validation from running on a broken compile.
Hardware Impact: Runtime impact is 0 us. Offline verification only.

## Decision - Unity Log Audit Verified
Problem: The Unity log parser and batchmode helper were added and needed actual local validation.
Solution: Ran Python compile, focused gate tests, and aggregate v2 validation after adding the log-audit layer. Compile returned clean, gate tests passed 8/8, and aggregate validation returned PASS.
Rejected Alternatives: Treating the added log parser as self-evident was rejected; it now has explicit tests for clean, C# error, and shader error cases.
Scalability potential: Compile/import sanity now gates both low-end and GOD_MODE proof paths before runtime captures are trusted.
Hardware Impact: Runtime impact is 0 us. Unity editor import itself remains unavailable here because default install roots are absent.

## Decision - Python Cache Cleanup Timeout
Problem: Python compile/test execution may create `__pycache__` directories that add worktree noise.
Solution: Attempted cleanup under `Tools/UX`, but the shell wrapper timed out with no output. Cleanup state is unverified.
Rejected Alternatives: Claiming cleanup success was rejected because no command output returned.
Scalability potential: No runtime change.
Hardware Impact: Runtime impact is 0 us.

## Decision - Unity Report Update Tool
Problem: Manual edits to `UnityVerification_UX_ENGINEER.json` risk fake evidence paths, wrong check IDs, or top-level PASS promotion before all checks are complete.
Solution: Added `Tools/UX/update_unity_verification_report.py`. It updates one required check, requires existing evidence for PASS, rejects unknown check IDs, and can write an audit report. `run_unity_import_check.ps1` now updates `UNITY_IMPORT` after a real import/log audit.
Rejected Alternatives: Hand-editing report JSON was rejected. Auto-promoting top-level PASS from one import check was rejected because GC, Frame Debugger, FOV, and capture checks still exist.
Scalability potential: The updater can safely record low-end and GOD_MODE evidence one check at a time without breaking the required-check set.
Hardware Impact: Runtime impact is 0 us. Offline workflow hardening only.

## Decision - Unity Report Updater Verified
Problem: The updater changed the evidence workflow and needed focused validation.
Solution: Ran Python compile, focused gate tests, and aggregate validation. Compile returned clean, gate tests passed 10/10, and aggregate v2 returned PASS.
Rejected Alternatives: Assuming the PowerShell helper is correct without Python-side tests was rejected. The helper remains runtime-pending until Unity exists.
Scalability potential: Check-by-check evidence recording now supports TOASTER/MX350 and GOD_MODE proof without manual JSON mutation.
Hardware Impact: Runtime impact is 0 us. Offline validation only.

## Decision - Unity Report Updater CLI Tests
Problem: Helper-level tests proved check lookup, but the command-line updater also needed coverage for the actual workflow used by `run_unity_import_check.ps1` and integrators.
Solution: Added `Tools/UX/test_unity_report_update_cli.py`. It updates a temporary report with an existing evidence file, rejects PASS with missing evidence, and rejects top-level PASS while other required Unity checks remain incomplete.
Rejected Alternatives: Relying on helper tests only was rejected because subprocess argument parsing and file writes are part of the real workflow.
Scalability potential: CLI tests protect check-by-check evidence recording for MX350 and GOD_MODE captures.
Hardware Impact: Runtime impact is 0 us. Offline validation only.

## Decision - Updater Direct-Script Import Fix
Problem: The first updater CLI test run failed with `ModuleNotFoundError: No module named 'Tools'` because executing `Tools/UX/update_unity_verification_report.py` by file path sets `sys.path` to `Tools/UX`, not the repo root.
Solution: Patched the updater to insert the repository root into `sys.path` before importing `Tools.UX.validate_unity_verification_report`.
Rejected Alternatives: Changing tests to use `python -m Tools.UX.update_unity_verification_report` was rejected because `run_unity_import_check.ps1` invokes the file path directly.
Scalability potential: This protects all future evidence updates, including MX350 and GOD_MODE captures, from import-path failures.
Hardware Impact: Runtime impact is 0 us. Offline workflow fix only.

## Decision - Updater CLI Fix Verified
Problem: The direct-script import fix needed proof across the focused CLI tests and aggregate validator.
Solution: Ran Python compile, focused updater CLI tests, and aggregate validation. Compile returned clean, updater CLI tests passed 3/3, and aggregate v2 returned PASS.
Rejected Alternatives: Trusting the import-path patch without rerun was rejected; the failing test was kept as regression coverage.
Scalability potential: Stable CLI evidence updates now protect every required Unity proof check.
Hardware Impact: Runtime impact is 0 us. Offline validation only.

## Decision - Post-Test Cache Cleanup Timeout
Problem: The latest Python tests may have created `__pycache__` directories under `Tools/UX` or `Tools`.
Solution: Attempted safe PowerShell cleanup with resolved-path containment checks. The shell wrapper timed out with no output.
Rejected Alternatives: Claiming cleanup success was rejected.
Scalability potential: No runtime change.
Hardware Impact: Runtime impact is 0 us.

## Decision - Updater Repo-Root Path Resolution
Problem: `update_unity_verification_report.py` used relative default report/audit paths. If launched from outside the repo, as a PowerShell helper can be, it could read or write the wrong `Docs/AgentLogs` tree.
Solution: Changed default report/audit paths to repo-root absolute paths and resolved relative evidence paths against repo root for existence checks. Added a CLI test that launches from a temporary cwd while using repo-relative evidence.
Rejected Alternatives: Requiring integrators to `cd` into repo root first was rejected because the helper already knows the project path.
Scalability potential: Evidence recording for MX350/GOD_MODE captures stays tied to the correct project regardless of operator cwd.
Hardware Impact: Runtime impact is 0 us. Offline workflow correctness only.

## Decision - Updater Path Hardening Verified
Problem: The path-resolution patch changed CLI behavior and needed focused and aggregate verification.
Solution: Python compile returned clean. A 60s focused unittest attempt timed out with no output, then a 180s rerun passed 4/4. Aggregate validation returned PASS.
Rejected Alternatives: Treating the first timeout as a test failure was rejected because the same test passed cleanly with a longer wrapper timeout. Ignoring aggregate rerun was rejected because hashes and command coverage changed.
Scalability potential: Evidence update commands remain stable when launched from arbitrary directories for all required Unity proof checks.
Hardware Impact: Runtime impact is 0 us. Offline workflow only.

## Decision - Deterministic Python Cache Cleanup
Problem: Repeated ad hoc cache cleanup attempts through PowerShell timed out, leaving cleanup state unclear after direct unittest runs.
Solution: Added `Tools/UX/clean_python_cache.py` and `Tools/UX/test_python_cache_cleanup.py`. The cleaner removes `__pycache__` directories only under a repo-contained root and writes a cleanup report.
Rejected Alternatives: Continuing ad hoc shell deletion was rejected because it repeatedly timed out. Broad cleanup outside `Tools` was rejected to avoid touching unrelated agents' caches.
Scalability potential: Offline validation remains source-first and avoids bytecode churn across multi-agent runs.
Hardware Impact: Runtime impact is 0 us. Offline hygiene only.

## Decision - Cache Cleanup Report Path Fix
Problem: The first focused cleanup unittest failed because `remove_pycache_dirs()` reported removed paths relative to the repo root even when the cleanup root was a temporary test directory.
Solution: Changed removal reporting to use paths relative to the cleanup root.
Rejected Alternatives: Weakening the test to use only repo paths was rejected; the function should be reusable and deterministic for injected roots.
Scalability potential: Offline tooling is more robust for temp-root CI tests and repo-root cleanup.
Hardware Impact: Runtime impact is 0 us.

## Decision - Cache Cleanup Verified
Problem: The cleanup tool needed a clean rerun after the report path fix.
Solution: Python compile returned clean, focused cache cleanup test passed 1/1, and aggregate validation returned PASS with cleanup included.
Rejected Alternatives: Skipping aggregate rerun was rejected because the cleanup command is now in the aggregate command list.
Scalability potential: Offline validation can remove test bytecode noise deterministically.
Hardware Impact: Runtime impact is 0 us.

## Decision - Unity Environment Probe
Problem: Unity absence was noted in logs, but the aggregate validation report did not include a machine-readable environment probe.
Solution: Added `Tools/UX/probe_unity_environment.py` and `Tools/UX/test_unity_environment_probe.py`. The probe reads the required Unity version, scans `UNITY_EXE`, PATH, and normal install roots, writes `UI_UnityEnvironmentProbe_UX_ENGINEER.json`, and explicitly keeps runtime verification pending.
Rejected Alternatives: Failing aggregate validation when Unity is absent was rejected because aggregate validation is static/Python proof, not runtime proof. Claiming Unity unavailable from stale notes was rejected; the probe refreshes evidence.
Scalability potential: Runtime verification can start immediately when Unity appears, with the required version and candidate path recorded.
Hardware Impact: Runtime impact is 0 us. Offline environment evidence only.

## Decision - Unity Environment Probe Verified
Problem: The environment probe needed command proof and its generated result had to be read back.
Solution: Python compile returned clean, focused probe tests passed 3/3, aggregate validation returned PASS, and `UI_UnityEnvironmentProbe_UX_ENGINEER.json` was parsed with `python -m json.tool`.
Rejected Alternatives: Treating previous manual path checks as sufficient was rejected; the aggregate now emits a fresh machine-readable probe.
Scalability potential: The runtime blocker is now precise: Unity `6000.4.1f1` is required, no candidates are present, and the next integrator can provide `UNITY_EXE` or install Unity.
Hardware Impact: Runtime impact is 0 us. Runtime measurement remains absent until Unity exists.

## Decision - Aggregate Report Readback
Problem: A console PASS line is weaker than reading the generated aggregate and cleanup reports back from disk.
Solution: Parsed `UI_HardwareAdaptiveValidation_UX_ENGINEER.json` and `UI_PythonCacheCleanup_UX_ENGINEER.json` with `python -m json.tool`; scanned `Tools` for remaining `__pycache__` directories.
Rejected Alternatives: Trusting the aggregate stdout alone was rejected because generated report integrity matters under multi-agent churn.
Scalability potential: The report now confirms no missing artifacts, 24 tests executed, cleanup ran, and Unity runtime status stayed pending.
Hardware Impact: Runtime impact is 0 us. Offline evidence readback only.

## Decision - Unity Probe Version Matching
Problem: The Unity environment probe could list executable candidates but did not classify whether they matched required Unity `6000.4.1f1`. A wrong editor path would still be insufficient runtime evidence.
Solution: Added version inference from candidate paths, candidate detail records, required-version matching, explicit `--unity-path`, and statuses for required-version found, version mismatch, unknown version, and not found.
Rejected Alternatives: Treating any Unity executable as acceptable was rejected because Unity import proof must use the project-required editor version.
Scalability potential: Runtime validation can now distinguish a usable editor from a wrong install before import/profiler work begins.
Hardware Impact: Runtime impact is 0 us. Offline environment proof only.

## Decision - Unity Probe Version Matching Verified
Problem: The version-matching probe patch needed focused test, aggregate validation, and report readback.
Solution: Python compile returned clean, probe tests passed 6/6, aggregate validation returned PASS, and `UI_UnityEnvironmentProbe_UX_ENGINEER.json` parsed clean with new candidate detail fields.
Rejected Alternatives: Skipping report readback was rejected because the changed output schema needed confirmation on disk.
Scalability potential: The runtime blocker is now precise even if a wrong Unity version is installed later.
Hardware Impact: Runtime impact is 0 us. No editor runtime data exists.
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

## Extra Hardening - Stale Report Rejection
Problem: A report can remain green after source/spec changes if it does not carry hashes or if tests only rebuild in memory.
Solution: Added SHA-256 hashes to `UI_Readability_UX_ENGINEER.json` and `UI_ShaderSampleAudit_UX_ENGINEER.json`; expanded `test_hardware_adaptive_ui.py` to compare written report JSON against freshly built reports.
Rejected Alternatives: Trusting timestamps or human memory was rejected; both fail under multi-agent churn.
Scalability potential: CI can now reject stale UI proof artifacts before Unity validation.
Hardware Impact: Offline verification only; runtime impact 0 us.

## Aggregate Validation Runner
Problem: The validation path existed as several commands, which creates operator error and partial-proof risk.
Solution: Added `Tools/UX/run_hardware_adaptive_ui_validation.py`; it regenerates readability and shader reports, runs IconBaker self-test, runs the unit harness, records command exits/timings, and writes `UI_HardwareAdaptiveValidation_UX_ENGINEER.json`.
Rejected Alternatives: A Markdown-only runbook was rejected because it cannot fail CI.
Scalability potential: One command now validates Low/Middle/High/Ultra UI proof artifacts before Unity import.
Hardware Impact: Offline verification only; runtime impact 0 us.
