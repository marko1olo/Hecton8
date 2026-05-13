# AI_FUNNEL_NAV_POLISH Rationale

Status: PENDING VERIFICATION

Problem: Prompt names `FunnelModifierJob`, but current first-party source does not contain that symbol.
Solution: Treat `HectonMapMagicVegetationBridge.StringPullPathJob` as the live funnel/string-pull implementation because it is Burst, scheduled after abyssal path solve, and processes voxel/MapMagic path corridors.
Rejected Alternatives: Editing `Assets/AstarPathfindingProject/Modifiers/FunnelModifier.cs` was rejected because it is third-party vendor code, managed `List<Vector3>` code, and AGENTS forbids custom drift in complex third-party assets without explicit cleanup authority. Creating a brand-new AI navigation subsystem was rejected because no direct dependency may be invented during parallel batch work.
Scalability potential: Low uses existing capped path buffers and no extra allocations. Middle keeps Burst auto-vectorized scalar math. High can spend saved CPU on wider route lookahead or richer fauna steering after profiling. Ultra can raise visual navigation readability without changing gameplay authority.
Hardware Impact: Expected i3/MX350 gain is from replacing division/normalization-form math in the funnel-like job with `math.rcp`/`math.rsqrt` and avoiding vendor managed funnel paths; measured proof absent.
