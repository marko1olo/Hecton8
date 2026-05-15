# Rationale: UX_ENGINEER

Prompt ID: HARDWARE_ADAPTIVE_UI_BAKER
Evidence boundary: STATIC_SOURCE / STATIC_DOC / CLI_COMPILE. Runtime evidence is PENDING_UNITY_VERIFICATION.

## Decision - Active Evidence Restoration After Archive

Problem: Incoming remote history archived the active UX status/log/rationale/blocker and aggregate report files, while the current UX aggregate validator still requires active files for self-validation.
Solution: Recreate minimal active evidence files from current validation facts, keep Unity runtime status pending, and rerun the owner aggregate script before committing.
Rejected Alternatives: Reading archived batch logs as active proof was rejected because batch hygiene treats archived files as historical. Claiming Unity runtime proof was rejected because Unity is unavailable.
Scalability potential: Static TOASTER/GOD_MODE gates remain machine-checked; runtime tier captures still require Unity.
Hardware Impact: 0 us runtime impact. Offline evidence restoration only.

## Decision - Restore Pending Unity Verification Report

Problem: Active aggregate validation failed because `Docs/AgentLogs/UnityVerification_UX_ENGINEER.json` was missing after archive cleanup, causing the Unity report audit and updater tests to crash before they could enforce the pending runtime boundary.
Solution: Restored a pending-only Unity verification report with every required runtime check set to `PENDING` and empty `evidencePath` values. Added prompt metadata to the active log so status/log self-validation can identify this task.
Rejected Alternatives: Copying archived pass-like claims was rejected. Marking runtime checks as PASS without Unity evidence was rejected by the runtime evidence gate.
Scalability potential: Static TOASTER/GOD_MODE validation can self-validate again while runtime proof still requires Unity import, GCMonitor, Frame Debugger, Quest captures, MX350 capture, and GOD_MODE capture.
Hardware Impact: 0 us runtime impact. Offline evidence repair only.

## Decision - Active Evidence Repair Verified

Problem: Restoring the pending Unity report was necessary but not sufficient; the active aggregate had to prove that the report, status/log files, and test harness were coherent after archive cleanup.
Solution: Reran the owner aggregate, standalone aggregate validator, status/log validator, Unity verification report audit, broad `Tools/UX` discovery, cache cleanup, Unity environment probe, and whitespace check. The aggregate is back to PASS while all runtime checks remain PENDING with empty evidence paths.
Rejected Alternatives: Treating file restoration as proof without rerunning the aggregate was rejected. Filling runtime evidence paths with placeholders was rejected because the report validator must preserve the Unity evidence boundary.
Scalability potential: Static TOASTER/GOD_MODE gates are active again in the current tree; runtime visual proof remains blocked until Unity and captures exist.
Hardware Impact: 0 us runtime impact. Offline validation only.

## Decision - Prompt Source Blocker Hardened

Problem: Active `Docs/Tasks/CURRENT_BATCH.md` is missing, so the mandatory prompt extraction source cannot be satisfied from the active task folder. The UX prompt exists in archived Batch006, but that fallback was only a manual note.
Solution: Added aggregate fields for prompt-source status, path, and active batch existence. The validator now accepts the archived Batch006 fallback only when active `CURRENT_BATCH.md` is absent, and rejects missing prompt source or archived fallback when an active batch file exists.
Rejected Alternatives: Recreating a broad active master batch from archive was rejected because other active continuation agents are not necessarily Batch006 agents. Ignoring the missing active batch was rejected because the batch prompt protocol needs explicit evidence.
Scalability potential: Static UX gates remain active without polluting the active task root for other agents. The prompt-source blocker is machine-readable for integrators.
Hardware Impact: 0 us runtime impact. Offline validation only.
