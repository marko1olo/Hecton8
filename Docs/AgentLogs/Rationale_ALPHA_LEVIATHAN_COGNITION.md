# Rationale_ALPHA_LEVIATHAN_COGNITION

Status: PENDING VERIFICATION

## Decision 1

Problem: Batch asks for Alpha Leviathan stalking AI but forbids custom MonoBehaviours.
Solution: Extend `PredatorCognitionDomain` Burst job and the existing `FaunaBrain` managed-to-native bridge only.
Rejected Alternatives: A new `AlphaLeviathanStalker` component would violate the prompt and add scene wiring risk. A separate runtime manager would add singleton pressure and cross-agent dependency.
Scalability potential: Low uses byte phase + axis/radial math. Middle uses fog-ring tangent. High uses smoother rsqrt steering. Ultra can spend saved CPU on richer presentation/roar/IK without new authority.
Hardware Impact: i3/MX350 hot-path target remains native array reads/writes and scalar math; expected managed GC gain versus component orchestration is 0 B/frame.

## Decision 2

Problem: `PredatorData` is not a current source type; the real cognition data owner is `PredatorCognitionDomain`.
Solution: Treat the domain-owned SoA native banks as the predator data surface and add `NativeArray<byte>` phase state there.
Rejected Alternatives: Inventing a `PredatorData` type would duplicate source authority and risk interface drift. Extending `CognitionCore` would disturb its 64-byte layout.
Scalability potential: Byte lane is cheap on low hardware and leaves high-tier calculations free to improve visual stalking.
Hardware Impact: 256 byte session allocation for phase state plus sentinel metadata; no per-frame heap pressure.

## Decision 3

Problem: Current `FaunaBrain.cs`, `GlobalSignals.cs`, and `Hecton8.Core.asmdef` already contain uncommitted edits.
Solution: Preserve and patch around current working-tree state.
Rejected Alternatives: Reverting or checking out files is forbidden; blind overwrite would erase another agent's work.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact.
