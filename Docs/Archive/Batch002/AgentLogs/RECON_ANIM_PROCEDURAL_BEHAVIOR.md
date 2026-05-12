# RECON_ANIM_PROCEDURAL_BEHAVIOR

STATUS: PENDING VERIFICATION

## 2026-05-11 Recon
Command: `rg -n "Animator\.SetIKPosition|OnAnimatorIK" "C:\hades\Hecton8\Assets\_Project\Scripts" -g "*.cs"`
Result: no matches found. Exit code 1 from ripgrep means no existing Animator IK callsites in `Assets/_Project/Scripts/`.

Crab runtime static checks:
- `rg -n "Transform|\.transform|GetComponent|FindObject|GameObject" ProceduralCrabLegIKRuntime.cs` returned no matches.
- `rg -n "math\.acos|math\.sqrt|Physics\.Raycast|OnAnimatorIK|Animator\.SetIKPosition" ProceduralCrabLegIKRuntime.cs` returned no matches.
