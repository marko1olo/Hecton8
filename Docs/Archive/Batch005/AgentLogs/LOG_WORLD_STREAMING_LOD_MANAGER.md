# LOG_WORLD_STREAMING_LOD_MANAGER

## 2026-05-13 HLOD Impostor Swapper

What was wrong -> Streaming dehydration and hydration existed, but no deterministic HLOD bridge preserved far-field wreck/base visibility. Large construction prefabs still carried standard `LODGroup` components. PDA had no unloaded-HLOD POI feed. Audio had no chunk-level mute state for distant impostors. Active impostor count was absent from the streaming blackbox.

What was done -> Extended `WorldChunkResidencyManager` as the sole HLOD mutation owner; added fixed NativeArray SOA for active matrices, types, chunk IDs, spawn times, centers, sizes, flags, and cartography points; consumed hydrated/dehydrated signals in late-frame post-simulation; added Burst swap/fade/AUP-shift jobs; routed matrices to the HLOD instance culling/indirect rendering path; added 1.5 s dither fade-in/out; exported bounded PDA HLOD points; exposed `IsChunkImpostorAudioMuted(long)` through `IStreamingBackpressureService`; added `ActiveImpostorCount` telemetry and dump path; removed `LODGroup` components from six construction-final base/wreck/module prefabs; added immediate purge path for permanently destroyed chunks.

Cinematic Cheats used -> Chunk objects are replaced by matrix impostors. Fade is shader dither, not CPU material tweening. PDA far POIs are 16 fixed float4 points, not UI markers. Audio muting is represented as a residency flag exposed through the registry, not portal graph traversal.

Exact Microseconds saved -> Measured value: unavailable because Unity MCP compile is unavailable and `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` is blocked by 92 unrelated missing namespace/type errors. Estimated hot-path savings: 0 B GC per swap; about 2 us for 16 same-frame signal drains; about 4 us per bounded active-impostor swap scan outside render upload; one standard LODGroup registration/traversal path removed for each of six construction-final prefabs when streamed. Status remains `PENDING VERIFICATION`.

## 2026-05-13 Hardening Continuation

What was wrong -> First pass had two production-grade defects: streaming owned a direct concrete renderer type, and active HLOD matrices were uploaded every late frame even when unchanged. The first issue is fragile under parallel agent/import churn; the second wastes bandwidth on MX350-class hardware.

What was done -> Added `IStreamingHlodMatrixRenderer`, changed the residency manager to a serialized `MonoBehaviour` interface boundary, implemented the interface in the octahedral renderer, added active-impostor dirty/version tracking, added a native fade-out count gate, and cached the PDA streaming service lookup. Compute culling still dispatches against the persistent matrix buffer; CPU-to-GPU matrix upload only happens when the SOA mutates or fallback fade requires refresh.

Cinematic Cheats used -> Still the same lie: unloaded chunks are matrix impostors with shader dither, not live geometry. The improvement is that the lie now stays resident on the GPU instead of being re-sent every frame.

Exact Microseconds saved -> Measured value: unavailable. Estimated saving: removes one full `ActiveImpostors` matrix buffer upload per unchanged late frame; skips fade-cull job scheduling when no fade-out impostors exist. Verification: MCP validation 0 diagnostics on owned modified C# files; Unity Console has unrelated `GlobalDataVault` / duplicate Diegetic blockers; latest `dotnet build` timed out after 120 s. Status remains `PENDING VERIFICATION`.

## 2026-05-13 Generated-Project Hygiene And Final Verification

What was wrong -> `WorldChunkResidencyManager` still depended on `Hecton8.Core.Scheduling` extension methods. The generated Core project does not resolve that asmdef, so CLI verification showed a touched-file namespace error even though the logic only needed the existing `GlobalRegistry.JobAdmission` contract. Fallback HLOD fade also needed a renderer-state gate so CPU matrix refresh is forced only when compute-visible matrices are not active.

What was done -> Replaced Scheduling extension calls with local admission wrappers using `GlobalRegistry.JobAdmission` and cold static FNV-1a job hashes. Kept admission denial behavior and completion EWMA reporting. Added `IsUsingVisibleMatrixStream` to the octahedral renderer and made the residency manager force fallback uploads while fade-out impostors exist without compute-culling visible matrices. Repaired the cross-domain `GlobalDataVault` regression by removing reintroduced live relocation and `Relocatable` descriptors again.

Cinematic Cheats used -> HLOD remains a matrix/shader impostor system. The new pass keeps unchanged matrices resident on the GPU and refreshes CPU fallback fade only when needed.

Exact Microseconds saved -> Measured value: unavailable. Estimated saving is still bandwidth-driven: unchanged HLOD matrices avoid CPU-to-GPU upload; tiny synchronous HLOD swap/fade kernels now use `Run()` instead of immediate `Schedule()+Complete()`. CLI filtered verification is clean for `WorldChunkResidencyManager.cs`, `HectonOctahedralImpostorRenderer.cs`, and `GlobalDataVault.cs`. Full `dotnet build` still fails outside this domain on missing generated-project namespaces/types. Unity MCP is unavailable for final post-patch validation. Status remains `PENDING VERIFICATION`.

## 2026-05-13 Renderer Dropout Guard

What was wrong -> When compute-visible matrix streaming dropped out, fallback rendering could skip rebuilding `_instanceBuffer` if the impostor count matched the previous count. That is a stale-data risk during culling-service loss or dispatch failure.

What was done -> `HectonOctahedralImpostorRenderer` now records whether the previous frame used the visible matrix stream and forces fallback instance upload on dropout, while preserving the no-upload steady state for normal compute-culling frames.

Cinematic Cheats used -> The fallback remains the same cheap octahedral impostor lie. The guard only guarantees that the lie uses current matrices when compute culling is unavailable.

Exact Microseconds saved -> Measured value unavailable. Runtime steady-state is unchanged; extra upload cost occurs only on culling dropout and prevents stale visuals. Filtered CLI build is clean for owned files; full build remains blocked outside this domain; Unity MCP is unavailable.
