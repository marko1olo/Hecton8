# LOG_ANIMATION_IK

## 2026-05-11 - ANIMATION_IK Final Report

Status: PENDING VERIFICATION

What was wrong:
- Added-mass reciprocal path needed proof that zero or non-finite mass cannot produce Infinity.
- Landing weight lean was height-delta driven and did not explicitly project slope normal into the body frame.
- Leviathan spine IK still used `TransformAccessArray`/`IJobParallelForTransform` instead of a prebuilt native position buffer for the solve.
- Final build verification is blocked by external Construction/Physics, HabitatGraph, and SaveBinary compile errors outside the touched Animation/IK files.

What was done:
- Verified the zero-mass singularity guard in `HectonPlayerMotor.ResolveHydrodynamicAddedMassStatelessAcceleration`.
- Added projected slope lean in `ContextualPhysicalIkRuntime.ResolveSlopeLeanRadians` with `math.project`.
- Fed COM slope lean into `ContextualPhysicalIkRig` spine rotation through a 0.35 share using no-trig quaternion approximation and nlerp.
- Converted `ProceduralLeviathanSpineIK.SolveSpineJob` from `IJobParallelForTransform` to `IJobParallelFor`.
- Added `_vertebraWorldPositions` as a persistent `NativeArray<float3>` and snapshot Transform positions once before scheduling, so the Burst solve consumes native data.
- Re-ran scoped anti-bloat scans and final `dotnet build`.

Zero-Mass Singularity Guard code:

```csharp
float finiteMass = math.select(0f, mass, math.isfinite(mass));
float bodyMass = math.max(HydrodynamicMinimumEffectiveMassKg, finiteMass);
float addedMass = bodyMass * HydrodynamicAddedMassAccelerationScale;
float safeMass = math.max(HydrodynamicMinimumEffectiveMassKg, bodyMass + addedMass);
float invMass = math.select(math.rcp(bodyMass), math.rcp(safeMass), accelerating);
return SafeVelocity(safeForce * invMass, Vector3.zero);
```

Cinematic cheats used:
- Stateless added-mass scalar with clamped reciprocal instead of history-based hydrodynamic inertia.
- Triangle-wave tail/death drift instead of sine curves and corpse skeleton simulation.
- VAT frame blending in shader instead of CPU skeletons for far swim motion.
- Dominant-axis `_BreathingPhase` vertex offset instead of chest bone breathing.
- `_HitFlash` shader bloat/emission instead of animator hit transitions.
- `math.project` scalar slope lean instead of exact whole-body normal alignment.
- Four-point constrained tentacle IK instead of full iterative managed Transform FABRIK.

Exact microseconds saved, reported as engineering estimates pending profiler:
- Zero-mass guard: 0.8 us cost per call; prevents INF/NaN recovery failure.
- Stateless inertia history purge: 12 us saved by avoiding history read/write.
- Adaptive predator IK batching: 35 us saved per skipped predator solve.
- Triangle-wave tail/pole cheats: 2-5 us saved per active solve path.
- Death corkscrew fake: 8 us saved per corpse.
- VAT far swim: 20-60 us saved per visible far group.
- Shader breathing publish skip: 3 us saved on unchanged frames.
- Slope lean projection: 0.6 us per active IK entity; avoids extra raycast/exact quaternion alignment.
- Four-joint ragdoll handoff: 50+ us saved per corpse activation versus full ragdoll graph.
- Tentacle constrained Burst IK: 12-30 us saved per tentacle group.
- NativeArray vertebra positions: 5-12 us saved per active leviathan solve by removing TransformAccess from the job.

Scalability:
- Low / MX350: 10 Hz predator IK, sparse native solves, VAT/shader presentation carries far motion.
- Middle: limited near-body contextual IK and four-point constrained tentacle chains.
- High: 30 Hz near predator IK, richer shader bloat and VAT blend.
- Ultra: spend recovered CPU on visual overkill through material/VAT density, not bone count or unmanaged simulation drift.

Final verification:
- Scoped scan: no `foreach`, `string.Format`, `.ToString(`, interpolated strings, exact sqrt/normalize/distance offenders in touched hot paths. `new List` matches are cold reusable scratch fields on `ProceduralLeviathanSpineIK`.
- `git diff --check`: CRLF normalization warnings only for touched C# files.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`: FAIL, external compile wall. Latest errors are `ConstructionManager.cs` missing `Hecton8.Physics.SyncTransforms`, `HabitatGraphManager.cs` missing `TransitionHatchMeshState`, and `SaveBinaryPayloadCodec.cs` / `SaveBinaryStorage.cs` save-system errors. No diagnostics point at `ContextualPhysicalIkRuntime.cs`, `ContextualPhysicalIkRig.cs`, or `ProceduralLeviathanSpineIK.cs`.

Final Git Diff summary:

```text
Assets/_Project/Scripts/Fauna/ProceduralLeviathanSpineIK.cs    | 58 ++++++++++++++++------
Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs    | 10 +++-
Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs | 29 ++++++++++-
3 files changed, 77 insertions(+), 20 deletions(-)
```

Final Git Diff key changes:

```diff
--- a/Assets/_Project/Scripts/Fauna/ProceduralLeviathanSpineIK.cs
+++ b/Assets/_Project/Scripts/Fauna/ProceduralLeviathanSpineIK.cs
@@
-        private struct SolveSpineJob : IJobParallelForTransform
+        private struct SolveSpineJob : IJobParallelFor
         {
             [ReadOnly] public NativeArray<float> NormalizedBoneT;
+            [ReadOnly] public NativeArray<float3> VertebraWorldPositions;
@@
-            public void Execute(int index, TransformAccess transform)
+            public void Execute(int index)
             {
-                if (!transform.isValid)
+                float3 sourcePosition = VertebraWorldPositions[index];
+                if (!math.all(math.isfinite(sourcePosition)))
                     return;
@@
-            _pendingSpineHandle = IJobParallelForTransformExtensions.ScheduleByRef(ref job, _vertebraAccessArray, default);
+            _pendingSpineHandle = job.Schedule(_normalizedBoneT.Length, 8);
@@
+        private void SnapshotVertebraWorldPositions()
+        {
+            if (!_vertebraWorldPositions.IsCreated || _runtimeChain == null)
+                return;
+
+            int count = math.min(_runtimeChain.Length, _vertebraWorldPositions.Length);
+            for (int i = 0; i < count; i++)
+            {
+                Transform vertebra = _runtimeChain[i];
+                _vertebraWorldPositions[i] = vertebra != null
+                    ? (float3)vertebra.position
+                    : new float3(float.NaN, float.NaN, float.NaN);
+            }
+        }

--- a/Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs
+++ b/Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs
@@
+            float2 slopeLeanRadians = ResolveSlopeLeanRadians(in next, in entity);
@@
-            float pitch = math.clamp(dominantDelta * entity.ComLeanPitchRadians, 0.0f, entity.ComLeanPitchRadians);
-            float roll = math.clamp(-deltaDifference * entity.ComLeanRollRadians, -entity.ComLeanRollRadians, entity.ComLeanRollRadians);
+            float pitch = math.clamp((dominantDelta * entity.ComLeanPitchRadians) + slopeLeanRadians.x, -entity.ComLeanPitchRadians, entity.ComLeanPitchRadians);
+            float roll = math.clamp((-deltaDifference * entity.ComLeanRollRadians) + slopeLeanRadians.y, -entity.ComLeanRollRadians, entity.ComLeanRollRadians);
@@
+        private static float2 ResolveSlopeLeanRadians(
+            in ContextualPhysicalIkTargetFrame frame,
+            in ContextualPhysicalIkEntityState entity)
+        {
+            float leftBlend = math.saturate(frame.LeftFoot.Blend);
+            float rightBlend = math.saturate(frame.RightFoot.Blend);
+            float blendSum = leftBlend + rightBlend;
+            float hasFootNormal = math.select(0.0f, 1.0f, blendSum > 0.0001f);
+            float3 blendedNormal = (frame.LeftFoot.WorldNormal * leftBlend) + (frame.RightFoot.WorldNormal * rightBlend);
+            float3 slopeNormal = ContextualPhysicalIkMath.SafeNormalize(blendedNormal, new float3(0.0f, 1.0f, 0.0f));
+            float3 rootForward = ContextualPhysicalIkMath.SafeNormalize(
+                math.mul(entity.RootRotation, new float3(0.0f, 0.0f, 1.0f)),
+                new float3(0.0f, 0.0f, 1.0f));
+            float3 rootRight = ContextualPhysicalIkMath.SafeNormalize(
+                math.mul(entity.RootRotation, new float3(1.0f, 0.0f, 0.0f)),
+                new float3(1.0f, 0.0f, 0.0f));
+            float slopeForward = math.dot(math.project(slopeNormal, rootForward), rootForward) * hasFootNormal;
+            float slopeRight = math.dot(math.project(slopeNormal, rootRight), rootRight) * hasFootNormal;
+
+            return new float2(
+                math.clamp(-slopeForward * entity.ComLeanPitchRadians, -entity.ComLeanPitchRadians, entity.ComLeanPitchRadians),
+                math.clamp(slopeRight * entity.ComLeanRollRadians, -entity.ComLeanRollRadians, entity.ComLeanRollRadians));
+        }

--- a/Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs
+++ b/Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs
@@
+        private const float SpineSlopeLeanShare = 0.35f;
@@
-                    ProcessSpine(stream, SpineChains[i]);
+                    ProcessSpine(stream, SpineChains[i], in frame);
@@
+            quaternion slopeLeanRotation = ApproximateSmallEulerXzNoTrig(
+                frame.ComLeanRadians.x * SpineSlopeLeanShare,
+                frame.ComLeanRadians.y * SpineSlopeLeanShare);
@@
+                quaternion leanedDesiredWorldRotation = NormalizeQuaternionNoSqrt(math.mul(slopeLeanRotation, desiredWorldRotation));
+                desiredWorldRotation = ApproximateNlerpNoSqrt(desiredWorldRotation, leanedDesiredWorldRotation, normalizedT * weight);
```
