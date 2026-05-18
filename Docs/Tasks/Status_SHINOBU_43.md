# Status_SHINOBU_43

PROMPT IDENTIFIED: SHINOBU_43
DOMAIN: MATERIAL_RESPONSE_AND_WEAR_ARCHITECT
TASK COUNT: 20

## Authority Snapshot
- Primary directive extracted from `Docs/Tasks/CURRENT_BATCH.md` with CLI by `<AGENT_PROMPT id="SHINOBU_43">`; re-read after task 3 and after polish mandate.
- Domain boundary read from `Docs/Actual Domains of Project.txt`: material response, UberNoir PBR, wear/rust/moss/SSS, shader-buffer path.
- Binary payload ledger read: no material texture binding or corrosion-rate binary is active runtime-wired; only visual water/extinction payloads are proven active.
- Relevant mandates used: `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `DATA_Runtime_Struct_Layout_ARM64.txt`, `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`, `REND_GPU_Sovereignty.txt`, `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`, `REND_DescriptorBinding_Reality_Check.txt`, `STRM_Async_Asset_Upload_Texture_Settings.txt`, `DBG_Telemetry_Crash_Reporting_PostMortem.txt`, `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`, `TOOL_Designer_Facades_CSV_Binary_Bridge.txt`.

## Implemented Files
- `Assets/_Project/Scripts/Graphics/Materials/ShinobuMaterialResponseRuntime.cs`
- `Assets/_Project/Scripts/Graphics/Materials/Editor/UberNoirMaterialLabWindow.cs`
- `Assets/_Project/Scripts/Graphics/Materials/Editor/Hecton8.Graphics.Materials.Editor.asmdef`
- `Assets/_Project/Art/Shaders/Hecton8_UberNoir.hlsl`
- `Assets/_Project/Art/Shaders/Core/Hecton8_UberNoir.shader`
- `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` BufferID/SystemID reservation only.
- `Assets/_Project/Scripts/Graphics/Materials/Hecton8.Graphics.Materials.asmdef` material-domain unsafe/package references.

## Loop 1 - Tasks 01-05
- [x] Task 01: Scanned `Docs/Archive`, `Assets/_Project/_Archive`, `StreamingAssets`, and material logs. DOD: CLI archive grep and payload ledger check. Rejected: wiring absent `corrosion_rates_007.bin` fantasy. Microseconds: emergency mock rates avoid file probe during render path, estimated 2-8 us/frame saved.
- [x] Task 02: Audited SHINOBU runtime/editor files for `Material.SetFloat`, `Material.SetColor`, `Renderer.material(s)`, and MPB. DOD: focused `rg` returns clean for new SHINOBU files. Rejected: per-material mutation. Microseconds: preserves SRP Batcher; estimated 80-400 us/frame in dense base scenes.
- [x] Task 03: DTOs use public fields, no `{ get; set; }`, no `Pack=1/4`. DOD: static scan clean. Rejected: property wrappers and defensive struct copies. Microseconds: avoids hidden copies in NativeArray loops, estimated 4-12 us/8192 rows.
- [x] Task 04: `GlobalShaderConstantsDTO` is 48B: float4 SSS at 0, float4 caustic at 16, float wear at 32, uint pads at 36/40/44. DOD: runtime `UnsafeUtility.SizeOf` guard. Rejected: loose Vector4 globals via Shader.SetGlobalFloat/Vector. Microseconds: one CBuffer bind instead of scattered globals, estimated 5-20 us/frame.
- [x] Task 05: Added `MockBiomassDensitySignal` and `MockBiomassScalarJob`. DOD: Burst exact flags and deterministic `Unity.Mathematics.Random`. Rejected: direct dependency on Agent 14 ecosystem. Microseconds: no cross-domain polling, estimated 10-30 us/frame saved.

## Loop 2 - Tasks 06-10
- [x] Task 06: UberNoir now declares albedo/normal/mask `Texture2DArray`, `StructuredBuffer<H8UberNoirMaterialStateDTO>`, and `materialIndex` from `SV_InstanceID`. DOD: HLSL binding exists and shader properties added. Rejected: material variants per texture set. Microseconds: avoids SetPass churn; estimated 100-500 us/frame in repeated structures.
- [x] Task 07: Added mask/noise-driven clean/rust/moss blending with WearAge, SaltAccumulation, BioGrowthMask. DOD: three-layer blend in shader. Rejected: tint-only rust. Microseconds: CPU simulation cost 0, shader ALU gated by quality.
- [x] Task 08: Added Dear Lie caustic shimmer: low tier triangle wave, high tier procedural/texture blend using AUP-stable coordinates. DOD: no volumetric caustic simulation. Rejected: raymarched volumetric caustics. Microseconds: estimated 200-900 us/frame saved versus volumetric pass.
- [x] Task 09: Added wrapped diffuse SSS using `_H8UberNoirSubsurfaceColor` and material moss/thickness mask. DOD: quality-gated SSS in both low/full lighting paths. Rejected: screen-space SSS pass. Microseconds: avoids extra fullscreen pass; estimated 150-600 us/frame saved.
- [x] Task 10: Added anisotropic brushed-metal specular from mask/flags/tangent. DOD: tangent-stretched specular in lighting. Rejected: extra BRDF shader variant. Microseconds: uniform math only; avoids variant churn.

## Loop 3 - Tasks 11-15
- [x] Task 11: `GlobalQualityWeight` flows through 48B CBuffer and drives update cadence, simulation budget, triplanar blend, caustics, SSS, and moss detail. DOD: `math.lerp`/polynomial curves in C# and HLSL lerp gates. Rejected: low/high binary switch. Microseconds: at q=0.1 update cadence collapses near 5Hz, estimated 40-90 us CPU saved on material updates.
- [x] Task 12: Rust/salt/moss procedural positions subtract `_TotalUniverseOffset`; Burst depth uses local depth scalar in `MaterialPowerDTO`. DOD: no remaining `+ _TotalUniverseOffset` material/caustic uses. Rejected: raw world-space noise. Microseconds: correctness feature; prevents origin-shift shimmer.
- [x] Task 13: Texture-array memory tier is represented by quality-scaled telemetry and shader triplanar fade. DOD: no runtime `Texture.Compress`; CSV maps slices, actual asset tier loading remains content-owner pending. Rejected: runtime compression. Microseconds: avoids runtime compression stall; memory MB is tracked in blackbox.
- [x] Task 14: Emissive power-level path implemented via `MaterialPowerDTO` and packed visible GPU DTO. DOD: shader multiplies emission by `PowerLevel`. Rejected: material emission color mutation. Microseconds: preserves batching; estimated 30-120 us/frame in powered bases.
- [x] Task 15: Visible-only upload path uses `ShinobuMaterialVisibleIndices` into `ShinobuMaterialVisiblePayload`; placeholder indices are sequential until culling owner writes them. DOD: upload count is visible count, not full world count. Rejected: blind 50k upload. Microseconds: 8192 visible cap vs 50k full upload saves about 1.3 MB/frame PCIe traffic.

## Loop 4 - Tasks 16-20
- [x] Task 16: Boot/cold allocates one structured `GraphicsBuffer<MaterialVisibleDTO>` and one constant `GraphicsBuffer<GlobalShaderConstantsDTO>`; persistent CPU memory is DataVault handles. DOD: no private persistent NativeArray fields. Rejected: managed arrays and per-frame allocations. Microseconds: zero OS zeroing after boot.
- [x] Task 17: Added 300-frame `MaterialResponseTelemetryEntry` ring and dump to `Docs/AgentLogs/Dump_TECH_ART_DISPATCH.bin` on upload >1ms, layout fault, or non-finite state. DOD: 64B telemetry entries, 300 capacity. Rejected: console-only diagnosis. Microseconds: negligible normal path; forensic coverage present.
- [x] Task 18: Added `UberNoir Material Lab` EditorWindow sliders for Global Rust Rate, Caustic Intensity, SSS Translucency, Salt Line Depth, debug heatmap. DOD: writes to DataVault/CBuffer path, no material mutation. Rejected: ScriptableObject/runtime material edits. Microseconds: no runtime cost outside editor interaction.
- [x] Task 19: Added zero-GC byte parser for `Data/Visuals/texture_set_indices.csv`, guarded to Editor/Development polling and staged into DataVault scratch. DOD: no string split/LINQ; fallback mock remains when file absent. Rejected: JSON/File.ReadAllText in hot path. Microseconds: parser avoids heap churn; shipping poll disabled.
- [x] Task 20: Added shader heatmap override: red WearAge, green BioGrowth, blue SaltAccumulation. DOD: EditorWindow toggles debug mode through CBuffer. Rejected: material keyword/material swap debug. Microseconds: no SetPass or material clone cost.

## Verification
- [x] Static scan: new SHINOBU C#/editor files have no `Material.SetFloat`, `Material.SetColor`, `Renderer.material(s)`, `MaterialPropertyBlock`, `GraphicsBuffer.SetData`, `foreach`, LINQ, UnityEngine.Random, `Time.deltaTime`, `Time.frameCount`, `JobHandle.Complete`, or `Pack=1/4`.
- [x] `git diff --check` on touched files and SHINOBU logs: no whitespace errors; line-ending warnings only.
- [x] `dotnet build Hecton8.Editor.csproj --no-restore --no-dependencies`: PASS, one unrelated obsolete API warning. Caveat: current generated `.csproj` files do not list the newly added SHINOBU runtime/editor files, so Unity import remains the authoritative compile proof.
- [ ] `dotnet build Hecton8.Core.csproj`: BLOCKED BY DEPENDENCY. Attempt 1 failed in `LocalizationManager.PollBabelOverrideCsv`; attempt 2 failed in `SubmarineDynamicsRuntime.VolcanicUpdraftVault`; attempt 3 failed/timed out after reporting `VolcanicUpdraftVault.SafeNormalize` missing in `VolcanicUpdraftDirector`. No SHINOBU compile errors surfaced before the compile wall.
- [ ] Unity shader import / HLSL compile: PENDING. Unity executable exists under Hub, but an external Unity batchmode plus dotnet/csc processes are active, so no new SHINOBU import run was launched.
- [x] Self-audit and polish continuation written to `Docs/AgentLogs/LOG_SHINOBU_43.md`.

## Self-Audit Loops
- Loop 1: Prompt re-read and mandate scan before coding.
- Loop 2: DTO/layout/static mutation audit after runtime creation.
- Loop 3: HLSL AUP scan removed all material `+ _TotalUniverseOffset` uses.
- Loop 4: Quality continuum audit added cadence collapse and triplanar lerp.
- Loop 5: Post-compile-wall audit isolated unrelated compile failures and kept SHINOBU scope intact.
- Loop 6: Polish re-audit after mandate escalation. Runtime upload path now uses A/B `GraphicsBuffer` lanes plus `LockBufferForWrite`/`UnsafeUtility.MemCpy`; unchanged visible payloads/constants are not re-uploaded. HLSL quality collapse now prefers the SHINOBU CBuffer weight when active, gates texture arrays through a continuous blend, returns triangle caustics below q=0.25, and returns triangle macro-noise below q=0.3. Static scan remains clean for material mutation APIs, `SetData`, `Time.frameCount`, `Time.deltaTime`, `JobHandle.Complete`, `UnityEngine.Random`, LINQ, and `foreach`.
- Loop 7: Bootstrap/lifecycle re-audit after mandate escalation. SHINOBU no longer creates a hidden `MonoBehaviour`/`GameObject` host or calls `DontDestroyOnLoad`; runtime is a dispatcher-owned service registered through `GlobalRegistry`. Telemetry sampling now follows a continuous `GlobalQualityWeight` budget, and power flicker uses deterministic hash/triangle math instead of sine.
- Loop 8: Quality hysteresis re-audit. CPU simulation keeps raw `GlobalQualityWeight` for immediate load shedding, while shader CBuffer quality is smoothed with `math.lerp`, `math.step`, and a polynomial curve. HLSL caustic and macro-wear detail now use smooth transition bands and skip heavy branches below q=0.22.
- Loop 9: Texture vitality re-audit. UberNoir now adds shader-only rust pores, salt crystals, wet edges, and moss veins through AUP-stable ALU masks. The layer fades in continuously from q=0.05..0.18, rich value-noise detail starts only at q=0.24, and no new texture binding or material mutation was introduced.
- Loop 10: Compile-wall isolation re-audit. SHINOBU runtime moved out of parent Core assembly into `Hecton8.Graphics.Materials`; the editor facade moved into a material editor assembly. Runtime now recompiles with material-domain files, not the massive Core parent, once Unity regenerates project files.

## Loop 6 - Polish Continuation
- [x] Upload path hardened: replaced single-buffer/`SetData` style with double-buffered `GraphicsBuffer.LockBufferForWrite` and unmanaged memcpy. DOD: `rg "SetData\("` clean for SHINOBU runtime. Rejected: CPU-side managed upload staging and writing into the same GPU buffer still bound for read. Microseconds: avoids avoidable driver sync risk; estimated 20-120 us saved on upload-heavy frames.
- [x] Dirty gating added: visible material payload uploads only after simulation/CSV/mock changes or visible-count changes; constants upload only after quality/editor/flag changes. DOD: `_visiblePayloadDirty`, `_constantsDirty`, `_lastUploadedVisibleCount` present. Rejected: blind every-frame upload of unchanged material DTOs. Microseconds: q-low/static frames avoid 32B * visibleCount transfer; 8192 visible rows means up to 256KB/frame avoided.
- [x] Shader low-quality collapse tightened: texture arrays fade out below q=0.12, procedural caustics use triangle branch below q=0.25, macro wear noise uses triangle branch below q=0.3. DOD: HLSL scan finds `textureArrayBlend`, `H8UberNoirMaterialMacroNoise`, and q<0.25 caustic branch. Rejected: always evaluating high-tap noise and then lerping the result away. Microseconds: low-tier fragment ALU/texture pressure reduced; exact GPU us requires Unity profiler capture.
- [x] Thermal authority corrected: `H8UberNoirGlobalQualityWeight()` no longer takes `max(materialWeight, legacyWeight)`, so a stale high legacy global cannot block SHINOBU thermal degradation. DOD: function now selects CBuffer material weight when material flags are active, otherwise legacy global. Rejected: "highest quality wins" merge. Microseconds: not a direct CPU save; prevents overload when thermal dictator drops quality.
- [x] Verification honestly bounded: generated `.csproj` files currently do not list the new SHINOBU runtime/editor files, and a foreign Unity batchmode plus `dotnet` process are still active. DOD: compile not relaunched under competing process load. Rejected: claiming green Unity import without an actual import.

## Loop 7 - Bootstrap And Telemetry Re-Audit
- [x] Removed hidden scene host: `ShinobuMaterialResponseRuntime` is no longer a `MonoBehaviour`; no `new GameObject`, `AddComponent`, `DontDestroyOnLoad`, or `enabled = false` lifecycle remains. DOD: focused `rg` scan clean. Rejected: hidden Unity object lifetime. Microseconds: minor steady-state gain, but removes scene-object callback/lifetime risk.
- [x] Runtime ownership clarified: cold service allocation happens once from `RuntimeInitializeOnLoadMethod`, registers phase adapters through `GlobalRegistry`, and unsubscribes on shutdown/static reset. DOD: constructor/initializer readback plus cold allocation comments. Rejected: component-driven bootstrap. Microseconds: no hot-path allocation; editor reload behavior is less fragile.
- [x] Blackbox cost bounded: telemetry scan now uses `ResolveTelemetrySampleBudget(GlobalQualityWeight)`, 32-384 quality-curved samples with a 16-row forensic floor. DOD: method present and used in `RecordTelemetry`. Rejected: full visible-buffer scan every VisualSync. Microseconds: up to about 8160 DTO reads avoided at 8192 visible rows on low quality.
- [x] Deterministic visual fake hardened: per-row power flicker now uses hash-to-triangle math, and visible packing sanitizes non-finite wear/salt/bio/power/depth before writing the GraphicsBuffer payload. DOD: `math.sin` scan clean; `math.isfinite` guards present. Rejected: transcendental flicker and shader-only cleanup. Microseconds: small per-row ALU reduction plus NaN containment.
- [x] Re-ran static guard after Loop 7: no forbidden SHINOBU matches for material mutation APIs, `GraphicsBuffer.SetData`, `UnityEngine.Random`, `Time.deltaTime`, `Time.frameCount`, `JobHandle.Complete`, `Pack=1/4`, `foreach`, LINQ, hidden scene host calls, `MonoBehaviour`, or `math.sin`.
- [ ] Unity import/HLSL compile remains PENDING VERIFICATION. Current blocker: foreign `Unity.exe` PID 40220 is running `Hecton8.QA.Headless.Editor.Shinobu38QaWatchdogBatchRunner.Run`; earlier `dotnet/csc` processes also overlapped this audit, so a new compile/import pass was not launched.

## Loop 8 - Quality Hysteresis And Shader Work Shedding
- [x] Split CPU and shader quality authority: `MaterialRuntimeScalarsDTO.GlobalQualityWeight` remains raw for cadence/budget, while `GlobalShaderConstantsDTO.CausticSpeed.w` receives `_publishedShaderQualityWeight`. DOD: code readback at `ApplyQualityAndEditorTuning`. Rejected: smoothing CPU quality and delaying thermal shedding. Microseconds: preserves immediate CPU collapse under thermal pressure.
- [x] Added continuous shader-quality smoothing: `ResolvePublishedShaderQualityWeight` uses `math.step`, `math.lerp`, asymmetric rise/fall rates, and smooth polynomial interpolation. DOD: method present and CBuffer write uses it. Rejected: binary threshold flips in shader constants. Microseconds: avoids repeated threshold churn; exact GPU impact requires capture.
- [x] Replaced hard macro-wear threshold with `H8UberNoirSmoothRange01(0.22, 0.44, quality)`. DOD: HLSL readback confirms heavy value-noise is skipped below q=0.22 and blended in continuously. Rejected: `quality < 0.3` hard branch. Microseconds: low q removes value-noise sampling, middle q avoids visual pop.
- [x] Replaced hard caustic threshold with cheap/rich color blending over `H8UberNoirSmoothRange01(0.22, 0.36, q)`. DOD: HLSL readback confirms rich procedural caustics are not evaluated below q=0.22. Rejected: instant q=0.25 branch swap. Microseconds: avoids two value-noise layers below the lower band.
- [x] Static scan after Loop 8 remains clean for material mutation APIs, `SetData`, `Time.deltaTime`, `Time.frameCount`, `UnityEngine.Random`, `JobHandle.Complete`, LINQ, `foreach`, hidden scene host calls, and `math.sin`.
- [ ] Unity import/HLSL compile remains PENDING VERIFICATION because foreign `Unity.exe` PID 40220 is still active.

## Loop 9 - Texture Vitality Without New Texture Residency
- [x] Added `H8UberNoirWearVitality` shader DTO: rust pores, moss veins, salt crystals, wet-edge mask, and normal weight. DOD: HLSL readback shows no C# DTO/layout change and no new GraphicsBuffer/texture binding. Rejected: loading unproven material array tiers from disk or mutating material texture slots. Microseconds: avoids boot/runtime texture residency work; exact GPU delta pending shader import/profile.
- [x] Added continuous low-cost vitality curve: cheap triangle masks fade in across q=0.05..0.18. DOD: `H8UberNoirSmoothRange01(0.05, 0.18, quality)` present. Rejected: always-on micro-detail at q=0.0. Microseconds: preserves survival-tier collapse to nearly plain UV/PBR fetches.
- [x] Added rich vitality branch: two value-noise layers plus one hash crystal mask only start after q=0.24 and blend through q=0.58. DOD: branch returns before rich noise when detailWeight is zero. Rejected: evaluating rich pores/veins and lerping them away. Microseconds: low-q fragments skip the extra value-noise work.
- [x] Applied vitality to PBR channels, not just color: albedo tint, smoothness, occlusion, emissive moss edge, and high-quality micro-normal perturbation. DOD: `H8UberNoirApplyWearVitalityColor` and `H8UberNoirApplyWearVitalityNormal` are called from `H8UberNoirSampleSurface`. Rejected: tint-only "alive texture" fake. Microseconds: no CPU cost; high-q spends ALU inside the existing UberNoir pass.
- [x] AUP stability preserved: vitality uses `H8UberNoirMaterialStablePosition`, never raw accumulated world position. DOD: no new `+ _TotalUniverseOffset` use. Rejected: UV-only procedural movement that swims after origin shift. Microseconds: correctness feature; prevents visual jitter in 100km world.
- [ ] Unity import/HLSL compile remains PENDING VERIFICATION while foreign Unity PID 40220 owns the project.

## Loop 10 - Compile-Wall Domain Isolation
- [x] Moved `ShinobuMaterialResponseRuntime.cs` from `Assets/_Project/Scripts/Rendering` to `Assets/_Project/Scripts/Graphics/Materials`. DOD: old path absent, new path present. Rejected: leaving SHINOBU in parent `Hecton8.Core.asmdef`. Microseconds: no frame-time change; developer iteration avoids broad Core recompile when Unity regenerates project files.
- [x] Changed SHINOBU runtime namespace from `Hecton8.Core` to `Hecton8.Graphics.Materials`. DOD: static namespace scan confirms material-domain namespace. Rejected: hiding a domain runtime behind a Core namespace. Microseconds: compile-wall improvement, not runtime work.
- [x] Updated `Hecton8.Graphics.Materials.asmdef`: unsafe enabled and explicit package/core-infrastructure references added for Burst, Collections, Jobs, Mathematics, and DataVault memory types. DOD: asmdef JSON parses. Rejected: relying on parent Core assembly leakage. Microseconds: no hot-path effect; compile scope is smaller.
- [x] Moved `UberNoirMaterialLabWindow.cs` into `Assets/_Project/Scripts/Graphics/Materials/Editor` and added `Hecton8.Graphics.Materials.Editor.asmdef`. DOD: editor facade now references material runtime assembly directly. Rejected: global editor assembly coupling for SHINOBU tuning. Microseconds: editor-only compile isolation.
- [x] Static scans after move remain clean for material mutation APIs, `GraphicsBuffer.SetData`, `UnityEngine.Random`, `Time.deltaTime`, `Time.frameCount`, `JobHandle.Complete`, `Pack=1/4`, `foreach`, LINQ, hidden scene host calls, `MonoBehaviour`, `math.sin`, binary quality switches, and raw `_TotalUniverseOffset` addition.
- [ ] Unity import/project regeneration remains PENDING VERIFICATION because foreign Unity PID 40220 is still running `Hecton8.QA.Headless.Editor.Shinobu38QaWatchdogBatchRunner.Run`.
