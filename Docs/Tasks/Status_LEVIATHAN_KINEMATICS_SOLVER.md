# Status_LEVIATHAN_KINEMATICS_SOLVER

Status authority: PENDING VERIFICATION until Unity compile and runtime evidence exist.
Prompt: LEVIATHAN_KINEMATICS_SOLVER
Role: MOTION_ENGINEER
Domain: ECHELON 3 / FLORA, FAUNA & BIOTA / Leviathan Procedural IK
Batch source: Docs/Tasks/CURRENT_BATCH.md

## Hygiene

- [x] Prompt extracted from CURRENT_BATCH.md with CLI regex over full file | DOD: strict prompt isolation | Alternatives Rejected: MCP/basic reader because truncation risk | Estimate: 400 us
- [x] Status file was absent before creation | DOD: fresh batch state | Alternatives Rejected: reuse old logs because batch hygiene forbids stale memory | Estimate: 40 us
- [x] Relevant mandates selected before code | DOD: registry-first compliance | Alternatives Rejected: coding from prompt only because Burst/native rules are stricter than task text | Estimate: 120 us

## Task Checklist

- [ ] Task 1: Extend FaunaKinematicsRuntime, no singleton work
- [ ] Task 2: Consume Leviathan intended velocity vector
- [ ] Task 3: Add/align Hecton8.Animation.IK asmdef dependency to Contracts
- [ ] Task 4: Dead code hunt for Animator/SkinnedMeshRenderer on Alpha Leviathan path
- [ ] Task 5: Define SOA NativeArray<float4x4> LeviathanBones
- [ ] Task 6: Burst Verlet spine constraint solver using math.rsqrt
- [ ] Task 7: SDF terrain hugging for lower five segments
- [ ] Task 8: MapMagic 2D height fallback through vault interface
- [ ] Task 9: Upload LeviathanBones to GraphicsBuffer
- [ ] Task 10: Hook existing compute/GPU skinning path where available
- [ ] Task 11: Strike tail whip impulse with one-second terrain bypass
- [ ] Task 12: AUP shift safety for all segments
- [ ] Task 13: Math LOD: Low tier eight segments and SDF disabled
- [ ] Task 14: Zero-GC hot path audit
- [ ] Task 15: Omega compile check: verify math.rsqrt constraints

## Iteration Log

### Loop 0: Intake

- Read batch prompt and mandates. No code written yet.
- Compile status: PENDING.
