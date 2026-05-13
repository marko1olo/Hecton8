# RECON_PLAYER_TOOL_IK

Scan command:
`rg -n "Animator\.SetIKPosition|SetIKPosition\(|SetIKRotation\(" Assets/_Project/Scripts`

Result:
No `Animator.SetIKPosition`, `SetIKPosition(`, or `SetIKRotation(` call sites found under `Assets/_Project/Scripts`.

Decision:
PLAYER_TOOL_IK stays on the existing `ContextualPhysicalIkApplyJob` animation-stream path. Unity Animator IK was not introduced.
