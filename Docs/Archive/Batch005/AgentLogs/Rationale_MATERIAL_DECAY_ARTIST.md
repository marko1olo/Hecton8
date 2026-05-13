# MATERIAL_DECAY_ARTIST Rationale

STATUS: PENDING VERIFICATION

## Decision 0: Scope and Mandate Selection
Problem: Equipment corrosion is shader-only presentation damage and must not become runtime mesh deformation, decal spam, or per-object material cloning.
Solution: Use Visual Fake First: one shared packed rust atlas, scalar/global shader inputs, local-space shader math, and CBUFFER-compatible properties in Hecton_CoreLit.hlsl.
Rejected Alternatives: Rust decals add GameObjects/draws and violate prompt. Mesh pitting/deformation adds geometry and CPU/GPU cost for presentation-only truth. Material clones break batching and memory discipline.
Scalability potential: Low disables POM and UV distortion; Middle uses roughness/normal blend; High enables 4-step POM; Ultra can spend saved geometry cost on deeper shader depth and blood/wetness polish.
Hardware Impact: i3/MX350 avoids extra renderers, avoids new geometry, avoids per-frame GC. Expected CPU gain vs decal/deform path: 0.05-0.30ms depending object count; GPU cost remains texture/ALU gated and PENDING VERIFICATION.

## Decision 1: Signal Consumer Instead of Durability Producer Patch
Problem: The prompt required ItemDurabilityChangedSignal consumption, but ToolDurabilitySystem was not the authoritative equipment corrosion publisher for this path.
Solution: Reused PlayerInventory's existing ItemDurabilityChangedSignal publisher and added MaterialDecayRuntime as a SignalBus consumer. This follows the DOD event-lane pattern and keeps VFX decoupled from inventory/tools internals.
Rejected Alternatives: Patching ToolDurabilitySystem would duplicate an existing publisher and risk double audio/material events. Polling PlayerInventory directly would create a concrete dependency.
Scalability potential: Low only consumes event lanes and uploads scalar state. Middle/High/Ultra all share the same signal path; visual overkill happens in shader only when quality allows it.
Hardware Impact: i3/MX350 avoids component searches and inventory traversal per frame. Estimated CPU saved versus polling inventory/tools: 5-30us/frame depending inventory size.

## Decision 2: Tool Shader Consumer, Not New Materials
Problem: Existing held-tool placeholder materials used URP Lit and therefore could not see Hecton_CoreLit.hlsl changes.
Solution: Added Hecton8/Tools/DecayLit shader using Hecton_CoreLit.hlsl and retargeted the 12 existing tool placeholder materials to that shader. No extra materials, no decals, no material clones.
Rejected Alternatives: DecalProjector corrosion was explicitly forbidden. Creating duplicate rusted material variants breaks batching and authoring. Patching Unity's built-in URP Lit shader is not project-owned.
Scalability potential: Low uses base albedo/roughness/rust blend. Middle adds packed normal. High enables 4-step POM. Ultra can spend the saved material/draw-call budget on denser atlas art or higher-frequency authoring later.
Hardware Impact: i3/MX350 keeps renderer/material counts unchanged. Expected CPU gain versus per-tool decal renderers: 0.05-0.20ms in tool-heavy views; GPU cost is texture/ALU gated.

## Decision 3: Runtime Atlas Fallback
Problem: The shader needs a single shared packed rust detail map, but no authored 512 atlas existed in the project.
Solution: MaterialDecayRuntime creates one 512x512 RGBA atlas cold with NativeArray<Color32>, binds it globally to _RustDetailMap, and sets _RustDetailMap_ST to 1,1,0,0.
Rejected Alternatives: Per-object texture slots increase memory and authoring churn. Resources.Load would hide dependency management. Procedural per-frame noise would violate frame budget and determinism.
Scalability potential: Low/Middle/High/Ultra all share one atlas. Low ignores POM; High/Ultra spend the same atlas on deeper visual interpretation rather than extra VRAM.
Hardware Impact: i3/MX350 pays roughly 1MB RGBA plus mips once, no managed per-frame allocation. Estimated runtime upload cost is cold only.

## Decision 4: Clean-Path Early-Out
Problem: The first implementation sampled _RustDetailMap before checking rust == 0 and carried Time.time in the runtime vector, forcing unnecessary texture fetches and global uploads.
Solution: Moved the rust texture sample behind the rust == 0 return and replaced Time.time with a stable item-hash seed. Wetness remains the only expected per-frame runtime vector change while fading.
Rejected Alternatives: Keeping animated blood patch noise was not worth a guaranteed uniform upload every frame. Sampling the atlas for clean equipment wastes the common case.
Scalability potential: Low/Middle clean tools pay no rust texture fetch. High/Ultra get POM only after rust > 0.3.
Hardware Impact: i3/MX350 clean-tool path saves one texture sample per shaded pixel and prevents needless uniform traffic; expected GPU gain depends on visible tool pixels and remains PENDING CAPTURE.

## Decision 5: Compile Wall Handling
Problem: Unity compile is blocked by unrelated Core.Memory GlobalDataVault.cs errors and a missing Hecton8.Vehicles.VFX assembly in the shared multi-agent workspace.
Solution: Marked Task 18 blocked by dependency while keeping MaterialDecayRuntime validated with Unity validate_script and static shader/SRP scans recorded.
Rejected Alternatives: Reverting material decay work would not fix GlobalDataVault.cs. Claiming compile success would be false.
Scalability potential: No runtime scalability decision; this is integration state tracking.
Hardware Impact: No hardware impact. Verification remains pending until the shared compile wall is cleared.

## OMEGA POLISH CHANGES
Problem: Final anti-bloat audit found one unnecessary exact shader reconstruction and one mandated build check that fails outside this task domain.
Solution: Replaced rust tangent normal z `sqrt(1-dot(xy,xy))` with a cheap `1 - dot*0.5` visual fake before the world-normal safe normalize. Ran `dotnet build Hecton8.Core.csproj` as required; it fails in HectonUnderwaterVisuals.cs lines 7040/7103, not in MaterialDecayRuntime.cs.
Rejected Alternatives: Keeping exact normal reconstruction spends ALU for invisible precision. Editing HectonUnderwaterVisuals.cs is outside MATERIAL_DECAY_ARTIST scope and unrelated to the new material runtime.
Scalability potential: Low avoids POM/UV distortion and now avoids exact rust-normal sqrt. Middle uses the same cheap normal fake. High/Ultra still spend on POM depth only when rust > 0.3.
Hardware Impact: i3/MX350 saves one sqrt-equivalent shader operation per active rust pixel; exact microseconds require GPU capture and remain PENDING VERIFICATION.

### Cinematic Cheats Used
- Rust pits are POM/UV distortion from a packed atlas, not geometry deformation.
- Rust edge wear is fresnel/curvature math, not authored edge masks.
- Blood splatter is hashed glossy patch overlay, not decals.
- Wetness is waterline/recent-fade smoothness boost, not material swaps.
- Rust normal z is a cheap approximation normalized downstream, not exact reconstruction.

### Final Git Diff Summary
- Added Assets/_Project/Scripts/VFX/Materials/Hecton8.VFX.Materials.asmdef plus generated meta.
- Added Assets/_Project/Scripts/VFX/Materials/MaterialDecayRuntime.cs plus generated meta.
- Added Assets/_Project/Art/Shaders/Hecton_ToolDecayLit.shader plus meta.
- Updated Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl with _RustDetailMap globals and dynamic wear/POM helpers.
- Retargeted 12 existing Assets/_Project/Art/Materials/Tools/Mat_Tool_*_Placeholder.mat assets to Hecton8/Tools/DecayLit.
- Updated Docs/Tasks/Status_MATERIAL_DECAY_ARTIST.md and Docs/AgentLogs/Rationale_MATERIAL_DECAY_ARTIST.md.
