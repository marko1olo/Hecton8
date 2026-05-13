# Recon - LEVIATHAN_TENTACLE_IK

Status: PENDING VERIFICATION

Command:
`rg -n "SpringJoint|ConfigurableJoint|CharacterJoint|HingeJoint|Joint" "Assets/_Project/Scripts/Fauna"`

Result:
- No `SpringJoint`, `ConfigurableJoint`, `CharacterJoint`, or `HingeJoint` component references were found in `Assets/_Project/Scripts/Fauna`.
- Hits were naming-only or data-only:
  - `FaunaSimplifiedRagdollHandoff.cs` uses method/field names such as `ApplyJoint`, backed by Rigidbody references.
  - `FaunaTentacleConstrainedIk.cs` uses `JointPose` data structs for a 4-joint constrained IK presentation job.
  - `ProceduralCrabLegIKRuntime.cs` writes GPU joint matrices for indirect rendering.

Conclusion:
Fauna currently has no production Unity Joint tentacle dependency to remove. New work must avoid adding one.
