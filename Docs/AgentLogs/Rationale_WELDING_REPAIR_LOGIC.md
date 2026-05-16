# Rationale - WELDING_REPAIR_LOGIC

## Initial Scope Gate
Problem: Hull repair is currently a generic progress-bar workflow and does not erase Batch 006 HullDents authority.
Solution: Implement a gameplay/tools repair kernel that operates on GlobalDataVault.HullDents, keeps hit math AUP-aware, clamps dent depth, and emits typed repair signals.
Rejected Alternatives: A local MonoBehaviour dent cache would violate Data Vault sovereignty. Direct Transform.position-only repair would violate AUP precision mandates. A generic health-addition raycast path would preserve the original defect.
Scalability potential: Low uses cheap dent erase math plus pooled spark fake. Middle adds shader-reactive dent/rust fade. High adds richer GPU spark drift. Ultra adds compute-advection overkill without changing gameplay truth.
Hardware Impact: Expected low-end i3/MX350 gain is zero heap allocation and bounded 16-dent linear scan, estimated under 0.01 ms per active tool excluding Unity physics scheduling.

## Mandate Selection
Problem: Task crosses tool raycast, hull damage, vault data, AUP, VFX, and telemetry.
Solution: Read eight selected mandates before coding: tool/raycast, hull integrity feedback, AUP determinism, coordinate precision, zero GC, native jobs, crash telemetry, and GPU fluid VFX.
Rejected Alternatives: Reading every registry file would waste time and increase irrelevant context. Reading only AGENTS.md would miss concrete kernel and VFX contracts.
Scalability potential: Mandates define Low/Middle/High/Ultra behavior and keep repair logic cheap while spending saved time on tiered visuals.
Hardware Impact: Mandate-driven bounded data access avoids heap churn and avoids per-frame full simulation on MX350.

## Vault Authority
Problem: Hull dents existed as shader-presenter private state, so repair could not erase the authoritative dent vector set.
Solution: Added BufferID.HullDents and mirrored HullDentShaderController damage writes into GlobalDataVault float4[16]. RepairTool reads/writes that vault lane directly.
Rejected Alternatives: Keeping _dentBuffer as truth would preserve the defect. Creating a RepairToolManager singleton would violate the purge task and add global coupling.
Scalability potential: Low/Middle/High/Ultra all share one 16-slot truth buffer; high tiers spend visual work downstream without changing gameplay data.
Hardware Impact: MX350 path is a 16-entry linear scan and one shader vector-array upload, estimated under 10 microseconds for repair-side math.

## Packed W Contract
Problem: The prompt says HullDents[i].w -= dt * RepairRate, but the existing shader packs radius and depth into w; raw subtraction would corrupt radius bits.
Solution: Unpack depth, subtract repair depth delta, clamp finite/non-negative, then repack radius and new depth into w.
Rejected Alternatives: Direct float decrement of packed w would make dent radii collapse unpredictably. Splitting radius/depth into a new buffer would break the existing shader contract.
Scalability potential: Low keeps packed scalar math. Middle/High/Ultra preserve richer dent radius while depth fades, enabling visual overkill without data migration.
Hardware Impact: Integer pack/unpack cost is trivial beside Unity raycast and shader upload; expected low-end gain is correctness with no heap allocation.

## AUP Local Conversion
Problem: Float-only Transform.InverseTransformPoint loses precision after floating-origin shifts and violates the double3 mandate.
Solution: Convert hit and submarine root to AUP double3, subtract in double precision, then rotate/scale into submarine local space.
Rejected Alternatives: Direct Transform.InverseTransformPoint was simpler but not deterministic enough for far-origin repair hits.
Scalability potential: Same math works for toaster and high-end; high-end gets no divergent gameplay truth.
Hardware Impact: Two double3 conversions and one quaternion inverse per active weld tick are below suspicious frame-time thresholds; estimated under 5 microseconds outside engine calls.

## Signal Decoupling
Problem: Gas sealing needed repair completion without RepairTool depending directly on GasDynamicsSolver.
Solution: Added HullRepairedSignal and had GasDynamicsSolver drain it only when no gas job is running, clearing the Breached room flag.
Rejected Alternatives: Direct GlobalRegistry.Gas call from RepairTool would create cross-domain coupling and possible job-state writes.
Scalability potential: Low uses one compact 64-byte signal. High/Ultra can add additional feedback consumers without modifying the repair kernel.
Hardware Impact: Signal drain is bounded by frame signal count and room flag writes are O(1); cheap devices avoid any room scan.

## VFX Tiering
Problem: Repair sparks needed both low-tier fake feedback and high-end compute advection without adding a new particle system allocation.
Solution: Reused DebrisSpawnSignal with AUP position, ToolSparks flag for generic sparks, and ComputeShard flag for CarveDebrisComputeRenderer StructuredBuffer injection.
Rejected Alternatives: Creating a dedicated welding compute buffer in RepairTool would duplicate the debris renderer and increase ownership conflicts.
Scalability potential: Low shows simple spark particles; Middle/High/Ultra route the same AUP signal into compute shard motion and current drift.
Hardware Impact: MX350 sees a bounded signal and low particle count; high-end receives higher visual value from the existing GPU buffer path.

## Validation Wall
Problem: dotnet build could not reach a clean project compile because Hecton8.Core currently fails on missing cross-assembly contracts before Assembly-CSharp diagnostics.
Solution: Ran restored Assembly-CSharp build and a no-reference target pass; recorded dependency wall and checked emitted errors for touched files.
Rejected Alternatives: Editing missing MacroDatabase, bucketing, world pager, and foveation contracts would be architectural sabotage outside this prompt.
Scalability potential: No runtime scalability decision affected; this is integration dependency debt.
Hardware Impact: No hardware gain; compile is blocked by project graph state, not the repair kernel.

## Multiplatform Packing And Guard Rails
Problem: HullRepairedSignal was explicit-size but not explicitly packed, leaving avoidable ARM64/Quest layout ambiguity and no signal-level finite sanitizer.
Solution: Set HullRepairedSignal to StructLayout Explicit Pack=1 Size=64 and registered a SignalPayloadFiniteGuards lane that sanitizes HitAup and invalid negative room ids.
Rejected Alternatives: Trusting default packing or letting invalid AUPs reach mobile GPU-adjacent consumers was too fragile.
Scalability potential: Low/Middle/High/Ultra all receive the same 64-byte signal contract; higher tiers can add consumers without changing the lane ABI.
Hardware Impact: Quest/Android gain is deterministic signal stride and no padding surprises. Runtime cost is one cached guard branch per published repair signal, estimated under 1 microsecond.

## H-Phi Data Sovereignty Pass
Problem: The hull repair lane still had too many opportunities to look like local NativeArray ownership even though the Vault is authoritative.
Solution: RepairTool and HullDentShaderController now cache VaultBufferHandle<float4>, resolve short-lived views only under TryLockBuffer/TryUnlockBuffer, and keep local shader Vector4[] only as upload staging for Shader.SetGlobalVectorArray.
Rejected Alternatives: Private NativeArray dent storage would violate data sovereignty. Re-fetching buffers every tick would add lookup churn and stale alias risk.
Scalability potential: Low keeps a 16-slot vault scan. Middle/High/Ultra can increase visual consumers while the gameplay truth remains a fixed vault lane.
Hardware Impact: i3/MX350 avoids heap allocation and repeated buffer lookup; estimated saved cost is 2-5 microseconds per active repair tick and lower lock misuse risk.

## AUP And NaN Vaccination Expansion
Problem: A second pass found float-only local conversions in repair visuals, hull dent presenter impact conversion, and structural sidecar point conversion, plus possible NaN normals/scales.
Solution: Replaced those point conversions with AUP double3 relative math, finite quaternion checks, safe scale division, and guarded rsqrt normalization. Repair power and intensity are clamped before use.
Rejected Alternatives: Leaving Transform.InverseTransformPoint in the lane would create mixed precision behavior after floating-origin shifts. Trusting lossyScale/Quaternion inputs was too brittle for mobile.
Scalability potential: Low gets predictable cheap math. High/Ultra get the same authoritative repair truth and can spend saved budget on VFX.
Hardware Impact: Added double3 conversion cost is estimated under 5 microseconds per active weld/contact path, traded for eliminating NaN/precision failure modes.

## Dear Lie And Visual Overkill Split
Problem: The initial spark path was valid but not explicit enough about toaster versus high-end behavior.
Solution: Low/MX350 emits 2-6 generic sparks while high tiers emit 8-32 compute-shard sparks into the existing CarveDebris StructuredBuffer/SDF/flow path. Hecton_FluidAdvection uses 64-thread groups and renderer clamps kernel group sizes to <=1024.
Rejected Alternatives: A single fixed burst count wastes low-end budget or undersells high-end machines. A dedicated welding compute shader would duplicate already-audited debris advection.
Scalability potential: Low: generic spark fake. Middle: shader unbend and rust fade. High: SDF/flow compute drift. Ultra: same signal can drive heavier downstream overkill without changing gameplay kernel.
Hardware Impact: Low tier saves an estimated 20-60 microseconds per active weld burst versus high-tier particle counts. High tier spends those cycles on visible compute-advection sparks.

## Validation Wall Second Pass
Problem: The second build gate still cannot validate Assembly-CSharp because the project graph is broken before the repair files compile.
Solution: Ran `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`; captured the failure class: RealtimeCSG.csproj missing many source files, plus unrelated Hecton8.Core missing VFX wake/light-shaft/ecosystem contracts.
Rejected Alternatives: Fabricating missing RealtimeCSG package sources or editing VFX/ecosystem service contracts would violate domain boundaries.
Scalability potential: No runtime scalability impact; this is repository dependency health debt.
Hardware Impact: No hardware gain; build remains blocked outside WELDING_REPAIR_LOGIC.
