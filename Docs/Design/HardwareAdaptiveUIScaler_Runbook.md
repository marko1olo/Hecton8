# Hardware Adaptive UI Scaler Runbook

Owner: UX_ENGINEER
Prompt ID: HARDWARE_ADAPTIVE_UI_BAKER
Status: UI SCALED - PYTHON STATIC VALIDATION PASS, UNITY PENDING

## Scope

This runbook is the operational entry point for the hardware-adaptive UI scaler evidence. It does not replace `HardwareAdaptiveUIScaler.md`; it lists the exact validation order and the boundary between local static proof and Unity-only proof.

## Local Validation

Run the aggregate validator from the repository root:

```powershell
python Tools/UX/run_hardware_adaptive_ui_validation.py
```

The aggregate validator runs:

```powershell
python Tools/UX/ui_readability_test.py --write-report
python Tools/UX/ui_shader_sample_audit.py --write-report
python Tools/IconBaker.py --self-test --output Docs/AgentLogs/IconBaker_UX_ENGINEER_SelfTest --manifest Docs/AgentLogs/IconBaker_UX_ENGINEER_SelfTest/IconBakeManifest.json
python Tools/UX/validate_unity_verification_template.py --write-report
python Tools/UX/validate_unity_verification_report.py --write-audit
python -m unittest Tools.UX.test_hardware_adaptive_ui Tools.UX.test_unity_verification_gates Tools.UX.test_unity_report_update_cli -v
```

The Unity evidence-template audit can also be run directly:

```powershell
python Tools/UX/validate_unity_verification_template.py --write-report
```

When Unity evidence is available, validate the filled runtime report directly:

```powershell
python Tools/UX/validate_unity_verification_report.py --write-audit
```

The Unity evidence-gate tests can also be run directly:

```powershell
python -m unittest Tools.UX.test_unity_verification_gates Tools.UX.test_unity_report_update_cli -v
```

Expected local artifacts:

- `Docs/AgentLogs/UI_Readability_UX_ENGINEER.json`
- `Docs/AgentLogs/UI_ShaderSampleAudit_UX_ENGINEER.json`
- `Docs/AgentLogs/UI_HardwareAdaptiveValidation_UX_ENGINEER.json`
- `Docs/AgentLogs/IconBaker_UX_ENGINEER_SelfTest/IconBakeManifest.json`
- `Docs/Design/HardwareAdaptiveUIScaler_UnityVerificationTemplate.json`
- `Docs/AgentLogs/UI_UnityTemplateAudit_UX_ENGINEER.json`
- `Docs/AgentLogs/UnityVerification_UX_ENGINEER.json`
- `Docs/AgentLogs/UI_UnityReportAudit_UX_ENGINEER.json`

## Unity Gate

Unity proof is separate. Static Python validation is not a replacement for import and render evidence.

Required Unity checks:

- Open the project with Unity `6000.4.1f1`.
- Confirm `Assets/_Project/Scripts/UI/WorldSpaceTMPSharpnessController.cs` imports cleanly.
- Open Console after import and record compile status.
- Run a HUD scene containing `O2 LOW` text and capture GCMonitor during steady state.
- Use Frame Debugger or RenderDoc to confirm the audited UI shaders stay at two texture/depth samples or less.
- Verify Quest 2 and Quest 3 FOV layout presets with the same camera FOV authority and hysteresis documented in `HardwareAdaptiveUIScaler.json`.

Fill `Docs/AgentLogs/UnityVerification_UX_ENGINEER.json` from actual Unity evidence. Do not edit the template into a pass report. Do not set report status to `PASS` until `Tools/UX/validate_unity_verification_report.py --write-audit` returns cleanly.

Batchmode import can be attempted with:

```powershell
Tools/UX/run_unity_import_check.ps1 -UnityPath "C:\Path\To\Unity.exe"
```

If a Unity import log already exists, audit it with:

```powershell
python Tools/UX/unity_compile_log_audit.py --log Docs/AgentLogs/UnityImport_UX_ENGINEER.log --write-report
```

To update one runtime evidence check without hand-editing JSON:

```powershell
python Tools/UX/update_unity_verification_report.py --check UNITY_IMPORT --status PASS --evidence Docs/AgentLogs/UI_UnityCompileLogAudit_UX_ENGINEER.json --actual "Unity batchmode import and compile-log audit passed." --write-audit
```

No local report may claim runtime completion until those Unity checks exist.

## Failure Handling

If the aggregate validator fails, do not edit reports by hand. Fix the source artifact, rerun the aggregate validator, then append the failure and fix to `Docs/AgentLogs/LOG_UX_ENGINEER.md`.

If Unity import fails, record the compiler error in `Docs/AgentLogs/LOG_UX_ENGINEER.md`, fix the compile wall manually up to three attempts, and mark `Docs/Tasks/Status_UX_ENGINEER.md` with `[BLOCKED BY DEPENDENCY]` only if the error is external to this UI domain.
