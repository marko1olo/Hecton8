# Manual Review Pass 10 - Fauna Bootstrap, Player Kinematics, And Extractor Owner Phases

Status: STATIC METHOD REVIEW - NO PLAY MODE / PROFILER PROOF
Date: 2026-06-02

## Reviewed Files

- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs`
- `Assets/_Project/Scripts/Ecosystem/EcosystemRuntimeInstaller.cs`
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
- `Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs`
- `Assets/_Project/Scripts/Construction/AutonomousExtractorSystem.cs`

## Findings

### 1. FaunaBrain Startup Caches Are Cold, But Material Clones Are Still A Crowd-Scale Proof Gate

`FaunaBrain.Awake()` calls `CacheBiolumPresentationLights()`, `EnsureFaunaPresentationMaterials()`, and `CacheLogicalLodComponents()` at `FaunaBrain.cs:792-806`. The scene hierarchy scans at `:4421-4439` and `:7431-7442` are startup cache routes, not direct evidence of per-frame scene search. That is better than a hot-path `GetComponentsInChildren` loop.

The unresolved risk is material ownership. `EnsureFaunaPresentationMaterials()` calls `_renderer.GetSharedMaterials(...)`, creates `new Material(sourceMaterial)` at `FaunaBrain.cs:4503`, and writes runtime material slots back through `_renderer.SetSharedMaterials(...)`. `ReleaseFaunaPresentationMaterials()` restores the original slots and destroys runtime materials at `:4548-4595`, but the clone still exists per fauna actor. This can be acceptable for a small number of hero creatures. It is not acceptable as the default crowd/ecosystem route without SRP Batcher, material count, and actor density proof.

Classification: `YELLOW_FAUNA_BOOTSTRAP_CACHE_OK_MATERIAL_CLONE_PROOF_REQUIRED`.

### 2. Fauna Collider Hygiene Is Correctly Rejection-Oriented, But LOD Collider Toggle Proof Is Still Required

`ValidatePrimitiveColliderRig()` rejects owned `MeshCollider` and requires a `CapsuleCollider` or `SphereCollider` at `FaunaBrain.cs:7756-7774`. This is aligned with the physics and generated-model bibles: fauna should not use high-poly visual meshes as physics truth.

The remaining risk is logical LOD presentation. `CacheLogicalLodComponents()` caches colliders once, and `ApplyLogicalLodPresentationState()` toggles `collider.enabled` at `FaunaBrain.cs:7460-7473`. The shape is cold-cached, but runtime collider enable/disable changes still need telemetry under fauna crowd stress to prove they do not create PhysX spikes or collision state bugs.

Classification: `GREENISH_PRIMITIVE_COLLIDER_POLICY_WITH_LOD_TOGGLE_PROOF_REQUIRED`.

### 3. EcosystemRuntimeInstaller Is A Bootstrap Recovery Route, Not A Production Scene Contract

`GameBootstrapper.PublishPlayerRuntimeReference()` calls `EcosystemRuntimeInstaller.EnsureRuntimeSystems()` at `GameBootstrapper.cs:7255`. The installer resolves or creates `__HECTON_ECOSYSTEM_RUNTIME` and adds `FaunaGeneticsManager`, `EcosystemHealthDirector`, `MigrationDirector`, and `EcosystemPopulationBalancer` if missing at `EcosystemRuntimeInstaller.cs:17-35`.

This is valid as a cold bootstrap/recovery lane. It is not sufficient as a production content contract. Release scenes should carry an authored bootstrap prefab or deterministic boot manifest so the installer does not hide missing scene composition.

Classification: `YELLOW_BOOTSTRAP_RECOVERY_ROUTE_AUTHORING_PROOF_REQUIRED`.

### 4. SomaticKinematicsRuntime Has Strong Owner-Phase Shape, With Forced Completion Proof Still Open

`SomaticKinematicsRuntime.LocalSimulationScratch.Ensure()` creates fixed persistent buffers for state, sphere, hand history, tuning, drag LUT, signal scratch, black box, and black-box cursor at `SomaticKinematicsRuntime.cs:882-935`. `Awake()`/`OnEnable()` prepare native state and local scratch at `:1014-1037`. `FixedTick()` schedules `SomaticKinematicsJob` once per fixed frame at `:1071-1094`; `PostFixedTick()` completes and publishes at `:1100-1104`.

This is the right architectural shape: dispatcher-owned fixed/post-fixed phases, fixed-size buffers, signal publication, and a black-box dump path. The open proof is not a static code failure; it is runtime evidence. `CompleteScheduledKinematicsInPostFixedOrShutdown(true)` is called in teardown/origin-shift paths and `PostFixedTick()` uses forced completion after scheduling. It must be profiled under bad physics/current stress to prove the job is small enough and not a hidden same-frame stall.

Classification: `GREENISH_OWNER_PHASE_FIXED_BUFFER_WITH_COMPLETION_PROOF_REQUIRED`.

### 5. AutonomousExtractorSystem Is Fixed-Capacity SlowTick SOA, But Needs Module Stress Proof

`AutonomousExtractorSystem` owns a fixed `AutonomousExtractorModule[MaxModuleCapacity]` and `ExtractorNativeState` persistent arrays sized to `MaxModuleCapacity = 256`. `EnsureExtractorNativeStateCold()` allocates the native arrays once at `AutonomousExtractorSystem.cs:720-724`. `SlowTick()` compacts modules, fills SOA job input arrays, schedules `AdvanceExtractionJob`, and defers completion through `TryCompleteScheduledExtractorJob(forceComplete: false)` on subsequent slow ticks. Teardown uses `forceComplete: true`.

The architecture is acceptable for the construction bible: fixed capacity, slow cadence, Burst job, no dynamic managed module list growth. The required proof is module stress: 256 modules, registration/unregistration churn, power network and persistent dropped item routing, and confirmation that slow-tick job completion never leaks into frame spikes.

Classification: `GREENISH_FIXED_CAPACITY_SLOWTICK_WITH_STRESS_PROOF_REQUIRED`.

## Blocker Changes From Pass 10

- Strengthen `RB-007`: the issue is specifically per-fauna material clone/SRP batching proof, not only generic material mutation.
- Strengthen `RB-008`: the installer is legal bootstrap recovery, but production acceptance requires authored root/boot manifest proof.
- Add `RB-126`: player kinematics and autonomous extraction owner-phase stress proof.

## Current Honest Verdict

This pass found two relatively strong runtime-owner shapes (`SomaticKinematicsRuntime`, `AutonomousExtractorSystem`) and two yellow production-policy risks (`FaunaBrain` material clones, `EcosystemRuntimeInstaller` dynamic root repair). No release acceptance is closed because no Play Mode, Profiler, GC, native memory, or device proof was run.
