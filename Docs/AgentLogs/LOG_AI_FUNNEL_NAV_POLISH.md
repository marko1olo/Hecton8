# AI_FUNNEL_NAV_POLISH Log

## 2026-05-14 - Funnel Rsqrt / Pack=1 Polish

What was wrong:
- Prompt named `FunnelModifierJob`, but no first-party symbol with that name exists.
- Vendor A* funnel code is managed third-party code and was not the domain target.
- Live first-party funnel-like path smoothing is `HectonMapMagicVegetationBridge.StringPullPathJob` in `VegetationFlowFieldIntegrator.cs`.
- Hot path contained normalization/division-form math candidates and no packed portal value.

What was done:
- Added `NavPortal` as `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`.
- Reworked portal construction through `BuildNavPortal` with finite and zero-width clamps.
- Replaced normalization-form math with `NormalizeRsqrtOrFallback` using `math.rsqrt`.
- Replaced hot divisions with `math.rcp` in obstacle weighting, threat grid indexing, DDA, and voxel local coordinate conversion.
- Expanded scalar triple product arithmetic for 3D winding instead of flattening to 2D.
- Preserved existing `NativeArray<Vector3>`/`NativeList<Vector3>` boundary contract; internal math uses `float3`.
- Kept `[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`.

Cinematic Cheats used:
- Predictable cheap 3D string-pull math instead of physical path relaxation.
- Rsqrt/rcp approximation path where exact sqrt/divide precision is not visually meaningful.
- 32-byte packed portal value for cache discipline.
- Rejected 2D projection cheat because underwater 6DOF vertical corridors can invert wedge logic.

Exact Microseconds saved:
- PENDING VERIFICATION. No Unity/Burst profiler session is available, and `dotnet build` is blocked by unrelated global dependencies. Fake microsecond values rejected.

Verification:
- Static scan of `StringPullPathJob` found no `math.normalize`, `math.length(`, `math.distance(`, or raw `/` matches in the edited job range.
- `dotnet build Hecton8.Core.csproj --no-restore /m:1 /nr:false` failed with 131 unrelated missing namespace/type errors; no reported error referenced `VegetationFlowFieldIntegrator.cs`.
- Unity MCP `validate_script` failed with `no_unity_session`.

Integrator notes:
- Assembly/data-vault/telemetry items are blocked by missing or unstable contracts and should be owned by the navigation/integration layer, not patched through globals from this job.

## 2026-05-14 - Patient Recheck / LOD / Black Box Upgrade

What was wrong:
- Math LOD was documented as blocked, but the scheduler had a safe managed access point for `GlobalRegistry.ScalabilityTier`.
- `MaxSamplesPerSegment` existed but did not constrain the LOS/DDA cost.
- Exhausted DDA checks returned visible, allowing over-smoothing through unverified voxel space.
- Black Box telemetry was absent for the funnel owner.

What was done:
- Added tiered `MaxPortalLookAhead`: Low/Unknown/MX350 = 4, Mid = 8, High/Ultra = 16.
- Passed the budget into `StringPullPathJob` as a primitive field, no global access from Burst.
- Capped DDA steps with `MaxSamplesPerSegment`.
- Changed DDA exhaustion to fail closed.
- Added `AbyssalPathTelemetryEntry` as a fixed 64-byte packed native telemetry record.
- Added persistent 300-entry telemetry ring, schedule/complete timing, over-budget telemetry, finite-point scan, and NaN dump to `Docs/AgentLogs/Dump_AI_FUNNEL_NAV_POLISH.bin`.

Cinematic Cheats used:
- Low-tier route smoothing keeps more raw waypoints instead of paying for long corridor beautification.
- High-tier spends budget on 16-portal line-of-sight compaction for cleaner motion silhouettes.
- Long unverified LOS now rejects compaction instead of simulating a heavier physics proof.

Exact Microseconds saved:
- PENDING RUNTIME PROFILER DATA. The owner now records real `FunnelMs`; no fabricated value written.

Verification:
- Static scan remains clean inside `StringPullPathJob`: no `math.normalize`, `math.length(`, `math.distance(`, or raw `/`.
- `dotnet build Hecton8.Core.csproj --no-restore /m:1 /nr:false` still fails due unrelated global dependency errors. Latest run: 128 errors, none reported in the edited funnel files.
- Unity MCP `validate_script` failed due editor transport error at `127.0.0.1:8088/mcp`.
