# RECON_TECH_SCOUT

Agent: ASSET_SCOUT
Status: PENDING VERIFICATION

## 2026-05-12 ASSET_SCOUT Unity 6 Rendering Recon

Sources checked:
- Unity 6000.4.6f1 release page: https://unity.com/releases/editor/whats-new/6000.4.6f1
- Unity Manual, GPU Resident Drawer in URP: https://docs.unity.cn/6000.0/Documentation/Manual/urp/gpu-resident-drawer.html
- Unity Manual, Render Graph unsafe/compatibility APIs: https://docs.unity.cn/6000.0/Documentation/Manual/urp/render-graph-unsafe-pass.html
- Unity Manual, GPU occlusion culling in URP: https://docs.unity.cn/6000.0/Documentation/Manual/urp/gpu-culling.html

Local project state:
- `ProjectSettings/ProjectVersion.txt`: `6000.4.1f1`.
- Latest checked release page: `6000.4.6f1`, released 2026-05-05.
- `Assets/_Project/Data/URP_Low (PC_RPAsset).asset`: `m_GPUResidentDrawerMode: 0`, GPU occlusion in cameras `0`.
- `Assets/_Project/Data/URP_Medium (PC_RPAsset).asset`: `m_GPUResidentDrawerMode: 0`, GPU occlusion in cameras `0`.
- `Assets/_Project/Data/URP_High (PC_RPAsset).asset`: `m_GPUResidentDrawerMode: 0`, GPU occlusion in cameras `0`.
- `ProjectSettings/ProjectSettings.asset`: `m_StaticBatching: 1`.
- First-party RenderGraph code exists under `Assets/_Project/Scripts/Visor`, but multiple passes still use `AddUnsafePass`, `CoreUtils.SetRenderTarget`, imported persistent textures, or compatibility-style render target control.

Findings:
1. GPU Resident Drawer is not implemented in active URP tier assets. Unity requires Forward+ URP, compute shader support, SRP Batcher, `BatchRendererGroup Variants = Keep All`, and `GPU Resident Drawer = Instanced Drawing`. Local URP assets have SRP Batcher on but GPU Resident Drawer off.
2. GPU occlusion culling is not active in the checked URP assets. It should be evaluated only for dense static/blocked environment sets; open-ocean views can lose time to setup cost.
3. Static batching is enabled globally. Unity GPU Resident Drawer guidance says disabling Static Batching can speed up the GPU Resident Drawer path. This is a candidate only after a controlled rendering capture, not an ASSET_SCOUT setting change.
4. RenderGraph is partially adopted. Passes using `AddUnsafePass` prevent URP from optimizing/merging some render passes and can force unnecessary memory transfers. Convert first-party visor/fullscreen passes to raster/compute RenderGraph builders where the legacy target API is not required.
5. Current 6000.4.6f1 release notes do not introduce a new ASSET_SCOUT-grade GPU Resident Drawer optimization. They do flag a known D3D12 RenderGraph async-compute crash and a shader-variant compilation freeze issue. Do not upgrade from 6000.4.1f1 to 6000.4.6f1 as a blind performance fix.

Recommendations:
- Low/MX350: Prototype GPU Resident Drawer only in dense module/corridor/debris scenes with repeated meshes. Keep open-water/ocean scenes on current path until Frame Debugger proves net gain.
- Middle: Enable GPU occlusion per renderer only after Rendering Debugger shows culled draw cost beats setup cost.
- High: Use GRD to buy denser environment dressing, not permanently bloated base meshes.
- Ultra: Allow richer object density and longer LOD residency only after `Hybrid Batch Group` draw evidence and VRAM capture.

Status: PENDING VERIFICATION. No project settings changed.
