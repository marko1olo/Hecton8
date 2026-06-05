# 1896 ToolScreenDiegetic Shader Channel Audit

Agent: 1896  
Mode: REPORT_ONLY_STATIC_SHADER_CONTRACT_AUDIT  
Evidence class: STATIC_SOURCE / STATIC_DOC  
Unity/build/import/bake/PlayMode/profiler/screenshots/DataMonolith: NOT RUN

## Scope

Static audit of `Hecton_ToolScreenDiegetic` and related tool-screen source evidence. No source, asset, prefab, scene, binary, `.meta`, DataMonolith, or task file was edited.

Owned outputs:

- `Docs/Reports/Batch18/1896_TOOLSCREEN_DIEGETIC_SHADER_CHANNEL_AUDIT.md`
- `Docs/Reports/Batch18/1896_TOOLSCREEN_DIEGETIC_SHADER_CHANNEL_MATRIX.csv`
- `Docs/Tasks/Status_1896.md`
- `Docs/AgentLogs/Rationale_1896.md`
- `Docs/AgentLogs/LOG_1896.md`

First-20-minutes route blocker removed: product-face handheld tool instrumentation now has a static evidence boundary. It is not visually or runtime accepted.

## Authorities Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `shaders.md`
- `rendering.md`
- `tools.md`
- `ui.md`
- `UI_DIEGETIC_HUD_STANDARDS.md`
- `Docs/Reports/Batch18/1880_TOOL_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1888_PRODUCT_FACE_TEXTURE_CHANNEL_MANIFEST_AND_SHADER_AUDIT.md`
- `Docs/Reports/Batch18/1890_PRODUCT_FACE_MATERIAL_TEXTURE_VALIDATOR_IMPLEMENTATION.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/UI_Diegetic_Physical_Interfaces.txt`

`Docs/Actual Domains of Project.txt` was checked and produced no content. Narrow domain used: product-face tool screen shader/material channel contract.

## Static Finding

`Assets/_Project/Art/Shaders/Hecton_ToolScreenDiegetic.shader` exists and declares shader name `Hecton8/UI/ToolScreenDiegetic`.

Accepted static shader facts:

- `_ToolScreenTex` is the only texture sampled by the fragment shader.
- `_ToolScreenTex.rgb` is used as the live screen signal.
- `_ToolScreenTex.a` is not read.
- `_ToolFallback01` blends the output toward `_FallbackTint`.
- `_ToolHeat01` draws a bottom heat bar and drives critical flash threshold.
- `_ToolBattery01` draws a top battery fill.
- `_ToolFault01`, `_ToolCriticalFlash01`, and `_ToolTypeHue01` drive warning/type presentation.
- `_ToolVisualOverkill01` gates grid, data sweep, and secondary tint.
- `_H8GlobalQualityWeight` participates in VR comfort/tunnel dither math.
- The shader contains procedural fallback scanline modulation. This is not a scanline texture slot.

Blocked static shader facts:

- `_BaseMap`, `_MainTex`, and `_EmissionMap` are declared and have samplers, but the fragment shader never samples them.
- No normal, scratch, grime, wetness, glyph mask, MRAO, ORM, ARM, packed screen mask, or dedicated emission mask channel is sampled.
- `_ToolDistanceMeters` and `_ToolAmmoUnits` are declared in the shader but are not read by the fragment shader. The controller can render distance/ammo text into the RT; that is not a shader scalar effect.

## Material Assignment Evidence

The shader `.meta` GUID is `0eeb4e3c4a47c924eaa9056f9d429396`.

Scoped static search under `Assets/_Project/Art/Materials`, `Assets/_Project/Prefabs`, `Assets/_Project/Art/Generated`, and `Assets/_Project/Scenes` found no `.mat`, `.prefab`, `.asset`, or scene reference to that GUID.

Result: no live project-owned material assignment is accepted. The shader is source evidence only.

## Runtime Binder Evidence

`Assets/_Project/Scripts/UI/Tools/ToolDiegeticDisplayController.cs` is relevant source evidence:

- It uses `_ToolScreenTex`, `_MainTex`, `_BaseMap`, and `_EmissionMap` property IDs.
- It binds the same texture to all four slots via a `MaterialPropertyBlock`.
- Current shader source reads only `_ToolScreenTex`; alias binding does not make `_MainTex`, `_BaseMap`, or `_EmissionMap` accepted channels.
- It rents a fixed `ToolScreen_RT_256` render texture and uses a ToolUI camera.
- It stages UI text through fixed `char[96]` buffers and `TMP_Text.SetCharArray`.
- It renders AMMO, HEAT, DST, BAT, status, SCAN, and DECRYPT text into the RT path.
- It derives screen render cadence from `GlobalQualityWeight`: up to 6-frame cadence at low weight, 1-frame cadence near high weight.
- It applies a 2 second fallback hysteresis window.

This is static source review only. It does not prove component wiring, RenderTexture pool behavior, material binding, visual readability, GC, profiler cost, or screenshot quality.

## Channel Contract Decision

The 1888 gap is only partially closed.

Accepted:

- `Hecton_ToolScreenDiegetic` has a minimal static channel contract: `_ToolScreenTex.rgb = live display signal`. Alpha is unused. No packed map exists.
- Scalar visual inputs for heat, battery, fault, critical flash, type hue, fallback, and visual overkill are statically clear.

Still blocked:

- Production tool-screen material/channel contract remains `BLOCKED_CHANNEL_CONTRACT_REQUIRED`.
- There is no sampled channel for scratches, grime, wetness, emissive glyph masks, screen glass normal, oxygen hints, sonar hints, or packed screen masks.
- There is no project-owned material assignment using this shader.
- There is no Unity capture or runtime proof.

Do not author, relink, or promote a premium tool-screen material from `_BaseMap`, `_MainTex`, `_EmissionMap`, package materials, placeholder materials, default emission, or screenshots until the shader/material route is extended and proved.

## Allowed And Blocked Effects

Allowed from current static source:

- `_ToolScreenTex` display signal.
- Procedural fallback scanline modulation.
- Heat bar.
- Battery bar.
- Grid/data sweep under `_ToolVisualOverkill01`.
- Fault red pulse.
- Critical heat flash/inversion.
- Tool type tint.
- RT-rendered text for ammo, heat, distance, battery, scanner progress, decrypt progress, and status.

Blocked until source and material proof exist:

- scratch texture;
- grime texture;
- wetness texture or wetness channel;
- emissive glyph mask;
- glass normal/scratch normal;
- packed screen mask;
- oxygen hint;
- sonar hint;
- cockpit shared screen contract;
- Subnautica-level visual acceptance for the physical screen carrier.

## Handheld And Cockpit Readability Rule

Handheld tools:

- The screen must be a physical screen mesh or glass/display slot on an authored tool body, not a flat emissive rectangle.
- The minimum accepted source path is a project-owned material using `_ToolScreenTex` plus authored casing/glass/label material roles around it.
- Scanner/analyzer/repair/cutter/builder displays must sharpen a tool decision: scan confidence, heat retreat, battery/power, distance/range, fault/depth failure, or decrypt progress.

Cockpit displays:

- This shader is not accepted as cockpit-wide screen contract.
- Cockpit panels need physical carrier, depth/pressure/route/oxygen owner data, update cadence, and material proof under `UI_DIEGETIC_HUD_STANDARDS.md`.
- Future cockpit reuse of this shader must add panel-scale RT resolution/cadence policy and prove compact readability.

## Low / Middle / High / Ultra Consequences

Low / compact:

- Must keep readable tool state, owner truth, and physical screen carrier.
- Allowed current cheap path: `_ToolScreenTex` RT or authored fallback plus procedural scanline and simple heat/battery/status.
- Rejected: flat emissive rectangle, default emission, package material, or placeholder noise.

Middle:

- Requires material separation around the display: scratched glass frame, casing, rubber, labels, and readable screen text.
- Can add stronger RT text density and material dirt only through sampled/proved channels or authored surrounding material.

High:

- Can add sampled scratch/grime/wetness/glyph masks, richer warning response, and better screen/casing integration only after channel contract update.
- Must preserve the same owner truth and command semantics.

Ultra:

- Can add layered glass, condensation, subtle refraction, secondary glyph masks, and cinematic carrier detail.
- Ultra adds sensory overkill only. It must not reveal new gameplay truth, oxygen truth, sonar truth, or hidden target state unavailable to lower lanes.

## Future Shader/Material Requirements

Required before unblocking production tool screens:

1. Create or update a project-owned `MAT_*` material using `Hecton8/UI/ToolScreenDiegetic` or an approved successor.
2. Assign that material to actual tool screen mesh slots in held and world tool prefabs.
3. Keep `_ToolScreenTex` as the explicit display signal slot.
4. If premium screen wear is needed, add sampled slots with exact semantics. Proposed contract:
   - `_ScreenWearMask.r = scratch`
   - `_ScreenWearMask.g = grime`
   - `_ScreenWearMask.b = salt/dirt`
   - `_ScreenWearMask.a = wetness`
   - `_GlyphMask.a = emissive glyph/readout mask`
   - optional `_GlassNormalMap` for scratch/refraction normal
5. Declare import policy: screen signal RT or authored fallback, mask textures linear, normals as normal maps, UI/fallback textures with correct sRGB policy, no runtime texture generation.
6. Name source owners for oxygen, sonar, depth, scanner confidence, heat, battery, fault, and status if shown.
7. Prove no package/default/placeholder/debug/flat-color material route.
8. Provide handheld first-person and world-pickup captures at compact and normal tiers.
9. Provide cockpit capture separately before cockpit reuse is accepted.
10. Run Frame Debugger/profiler/GC proof if runtime render path, RT allocation, shader cost, or material update cost is claimed.

## Matrix

Detailed rows are in:

`Docs/Reports/Batch18/1896_TOOLSCREEN_DIEGETIC_SHADER_CHANNEL_MATRIX.csv`

## Verification Performed

Commands run after writing owned artifacts:

```powershell
git diff --check -- Docs/Reports/Batch18/1896_TOOLSCREEN_DIEGETIC_SHADER_CHANNEL_AUDIT.md Docs/Reports/Batch18/1896_TOOLSCREEN_DIEGETIC_SHADER_CHANNEL_MATRIX.csv Docs/Tasks/Status_1896.md Docs/AgentLogs/Rationale_1896.md Docs/AgentLogs/LOG_1896.md
Import-Csv Docs/Reports/Batch18/1896_TOOLSCREEN_DIEGETIC_SHADER_CHANNEL_MATRIX.csv | Measure-Object
Select-String -Path Docs/Reports/Batch18/1896_TOOLSCREEN_DIEGETIC_SHADER_CHANNEL_AUDIT.md,Docs/Reports/Batch18/1896_TOOLSCREEN_DIEGETIC_SHADER_CHANNEL_MATRIX.csv,Docs/Tasks/Status_1896.md,Docs/AgentLogs/Rationale_1896.md,Docs/AgentLogs/LOG_1896.md -Pattern 'ToolScreenDiegetic','emission','scanline','scratch','wetness','packed','Subnautica','PENDING UNITY'
```

Results are recorded in `Docs/AgentLogs/LOG_1896.md`.

## Result

What was wrong: 1888 correctly blocked `Hecton_ToolScreenDiegetic` because no shader/channel contract had been inspected. Static source now proves a narrow screen-signal contract, but it does not prove premium material channels or live assignment.

What I did: audited shader, controller/binder source, material GUID usage, prior Batch18 reports, UI/rendering/tool authorities, and wrote a matrix separating accepted static facts from blocked claims.

In-game result: PENDING UNITY. Unity, screenshots, PlayMode, profiler, Frame Debugger, import, and material inspector were not run by task order.

What was verified: static shader property use, sampled texture path, unused declared texture slots, scalar presentation path, RT binding source, absence of scoped material/prefab/scene shader-GUID references, CSV parse count, diff whitespace gate, and required term presence.
