# VAT / BRG Scatter Route Audit

Date: 2026-07-28
Status: PENDING VERIFICATION
Evidence class: `STATIC_SOURCE` / `FILESYSTEM`. No Unity import, Play Mode, Frame Debugger, profiler, GC,
or player-build proof was produced. The Unity slot was held by another owner for the duration of this
audit, so every runtime claim below is static-read only and labelled as such.
Owner domain: rendering / world ecology scatter
Authority: `AGENTS.md` `[RULE] Zero-GC Scatter & Animation Protocol`, `Runtime Hot-Path Law`,
`Global Systems Doctrine`, `Premium Approximation`; `rendering.md` `Offline Bake vs Runtime Generation Law`;
`.agents-skills/REND_Instanced_Flora_Physics.txt`; `.agents-skills/REND_GPU_Driven_Animation_VAT.txt`;
`.agents-skills/REND_GPU_Sovereignty.txt`; `3DMODEL_FLORA_CORAL.md`; `animation.md`;
`PROCEDURAL_ASSET_PIPELINE.md`.

## First-20 Route Hook

- First-20 moment: **First exit** and **World load** per
  `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md:76` — "Player exits into bright,
  beautiful, readable photic water with alien biota".
- Route blocker removed: the opening shallows currently have no scattered flora field, because the
  placement owner in `02_HECTON_WORLD` is a disabled component. That is the blocker this audit localises.
- Proof class: Play Mode capture of the shallow route, Frame Debugger for the draw path, Rendering
  Statistics SetPass/batch delta, GCMonitor, and compact-tier capture. None exist yet.

---

## 1. Executive Verdict

The mandated route is **not missing. It is built, mature, and unplugged.**

The law's wording ("must use offline baked VAT and BRG indirect rendering") implies two absent systems. In
fact:

| Piece | State |
|---|---|
| BRG / indirect draw infrastructure | **EXISTS**, production quality, Burst-jobbed, ~12.5 k lines across 4 owners |
| Generated flora meshes with LOD chains + vertex colours | **EXISTS**, 600 `GEN_*` mesh assets, 200 `LODGroup` prefabs |
| Offline Blender forge honouring the vertex-colour contract | **EXISTS**, `Tools/Blender/h8forge` |
| Manifest ingestion into Unity | **EXISTS**, `HectonFBXPostprocessor.cs` |
| Runtime wiring of the BRG renderers | **ABSENT** — zero scene/prefab/`AddComponent` binding |
| The GameObject placement owner | **PRESENT IN SCENE BUT COMPONENT-DISABLED** |
| GPU Resident Drawer | **DISABLED on every URP asset** |
| VAT baker for flora (kelp/coral) | **ABSENT** |
| VAT baker for fauna swarms | Exists, never run, and mis-wired (see §4) |

The single highest-value finding is that **no new rendering architecture is needed to get a legal kelp
field on screen.** The cheapest correct first step is a settings + authoring change, not code (§7).

---

## 2. What Exists — Census

All paths absolute-relative to `C:\hades\Hecton8`. Line numbers are read, not inferred.

> **Search-hygiene warning for anyone repeating this audit.** `C:\hades\Hecton8\.claude\worktrees\`
> contains stale full copies of the repo left by other agents. An unfiltered `rg` returns those first and
> silently exhausts the result limit — my first pass produced 60 hits that were *all* worktree copies and
> zero live-tree files. Always pass `--glob '!.claude/**'` or scope to `Assets` / `Tools`.

### 2.1 BRG and indirect-draw owners (live tree)

| File | Lines | What it is | Runtime? | Bound? |
|---|---|---|---|---|
| `Assets/_Project/Scripts/World/HectonBatchRendererGroupUtility.cs` | 433 | Shared BRG helper: Burst `BuildMatrixVisibilityMaskJob` frustum cull (`:21`), `FinalizeSingleDrawCommandOutputJob` writing real `BatchDrawCommand`/`BatchDrawRange` (`:96`), `TempJob` output allocation with finite guards (`:177`) | runtime, `internal static` | called by the vegetation renderer only |
| `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` | 7123 | The main flora renderer. `MonoBehaviour, ILateFrameTickable, ISlowTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener` (`:26`) | runtime | **NO** |
| `Assets/_Project/Scripts/World/GPUScatterDirector.cs` | 2433 | Indirect scatter submission (`:22`), `Graphics.RenderMeshIndirect` at `:682` | runtime | **NO** |
| `Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs` | 2828 | LOD/cull manager (`:126`), `RenderMeshIndirect` at `:1622` | runtime | **NO** |
| `Assets/_Project/Scripts/World/ScatterGPUIBackend.cs` | 166 | `GraphicsBuffer`-only indirect backend. Double-buffered `LockBufferForWrite` upload (`:66-87`), `IndirectDrawIndexedArgs` write (`:104-111`), `Graphics.RenderMeshIndirect` (`:129`), AUP origin-relative matrices (`:28-40`) | runtime, `internal sealed` | via `GPUScatterDirector` |
| `Assets/_Project/Scripts/World/ImpostorSystem.cs` | 2136 | Billboard/impostor HLOD | runtime | **NO** |
| `Assets/_Project/Scripts/World/HectonOctahedralImpostorRenderer.cs` | 796 | Octahedral impostor indirect renderer | runtime | **NO** |
| `Assets/_Project/Scripts/World/InstancedFloraRenderer.cs` | 14 | **Empty subclass** of `HectonIndirectVegetationRenderer` (`:11`) — a name alias with no body, created for "external graphics tasks" (`:6-7`) | runtime | **NO** |

This is a genuinely good implementation. `ScatterGPUIBackend.cs:59-60` even carries the canonical
`// COLD ALLOC:` comments required by `AGENTS.md` `Runtime Hot-Path Law`, and every buffer is
double-buffered with an upload-budget check (`:77`).

### 2.2 Liveness — the decisive measurement

`02_HECTON_WORLD.unity` (6,270,260 bytes), `010_TEST.unity`, and `020_RENDER_SANDBOX_V2.unity` are
**serialized as binary**, so a plain GUID text grep returns zero for every component whether it is present
or not. `Assets/_Project/Scripts/Editor/Diagnostics/H8_PlacementOwnerEnabledAudit.cs:26-29` documents this
exact trap. My first GUID sweep fell into it and produced a false "nothing is bound anywhere".

Corrected method: Unity's binary serializer stores the `.meta` GUID hex with the nibbles swapped inside
each byte. Scanning for that byte pattern, validated against two positive controls:

| Script | Binary-aware result in `02_HECTON_WORLD` |
|---|---|
| `WorldProceduralScatterDirector` | **PRESENT** (control 1 — independently corroborated by `H8_PlacementOwnerEnabledAudit.cs:15-17`) |
| `FloraAmbientSwayRuntime` | **PRESENT** (control 2 — also text-bound in three other scenes) |
| `HectonIndirectVegetationRenderer` | absent |
| `InstancedFloraRenderer` | absent |
| `GPUScatterDirector` | absent |
| `GpuScatterLodManager` | absent |
| `ImpostorSystem` | absent |
| `HectonOctahedralImpostorRenderer` | absent |
| `HectonHLODRenderer` | absent |
| `HectonDistantLandmarkRenderer` | absent |

Two positive controls firing on the same scan that returns absent for all eight renderers makes the
negative trustworthy, within the limit that a component added by `AddComponent` at runtime would be
invisible to any file scan.

The `AddComponent` half is also negative for runtime. The only construction site for any of them is
**Editor-only**: `Assets/_Project/Editor/Generators/World/AbyssalScatterPolisherPipeline.cs:644`
(`root.AddComponent<GpuScatterLodManager>()`).

And the runtime installer explicitly declines to install them —
`Assets/_Project/Scripts/World/WorldRuntimeInstaller.cs:20-23`:

> `/// This installer restores the three owners a bare runtime root can genuinely stand up. It does`
> `/// NOT install HectonWorldGenerator, HectonIndirectVegetationRenderer or GpuScatterLodManager -`
> `/// each of those needs authored assets or an authored producer that no runtime root can supply,`
> `/// and the per-system proof is recorded at the call sites below rather than summarised here.`

**Conclusion: the BRG/indirect flora renderers are dead code at runtime today.** Not wrong code — unwired
code.

### 2.3 A documented dead sub-path inside the live renderer

`HectonIndirectVegetationRenderer.cs:2651-2673` carries an unusually honest comment. Quoting the load-bearing
part:

> `/// DEAD PATH - and it must not be woken up without the work described below.`
> `/// This method is the ONLY place _batchRendererGroup is ever assigned non-null, and it has`
> `/// ZERO callers in the entire project. So the group is permanently null, OnPerformCulling`
> `/// never fires, _batchId and every BatchMeshID/BatchMaterialID stay at 0 [...]`
> `/// Everything actually drawn goes through RenderIndirectPass -> Graphics.RenderMeshIndirect with matProps.`
> `/// The trap: BatchRendererGroup DOES NOT CONSUME MaterialPropertyBlock.`

So even if this renderer were bound, its **actual** draw path is `Graphics.RenderMeshIndirect` +
`MaterialPropertyBlock`, not `BatchRendererGroup`. `EnsureBatchRendererGroupResources` (`:2674`) is real
BRG code that nobody calls.

That matters legally. `REND_Instanced_Flora_Physics.txt:14` reads
`[FORBID] Graphics.DrawMeshInstanced and manual Graphics.DrawMeshInstancedIndirect dispatch.
BatchRendererGroup / GPU Resident Drawer mandatory.` `REND_GPU_Sovereignty.txt:32` forbids raw
`DrawMeshInstancedIndirect` "unless the object family is explicitly not representable as GPU Resident
Drawer GameObjects and has a measured custom-culling win". `rendering.md:259` is the most permissive:
"Only `Graphics.RenderMeshIndirect` through BRG is acceptable."

`Graphics.RenderMeshIndirect` is Unity 6's successor API to `DrawMeshInstancedIndirect`, so the current
route is not the banned legacy call. But it is *not* "through BRG" either, and the sovereignty exception it
would need — named owner, measured custom-culling win, profiler proof — **has not been produced.** Status:
`PENDING VERIFICATION`, leaning non-compliant on the exception paperwork rather than on the API choice.

### 2.4 Generated flora asset data — better than expected

`Assets/_Project/Art/Generated/Flora/BioForge/` — **600 `.asset` Mesh files**, correctly `GEN_*`-prefixed
per `AGENTS.md:170`, in complete LOD triplets:

```
GEN_Shallows_Kelp_000_Flora_5A110101_LOD0.asset   (747,584 bytes)
GEN_Shallows_Kelp_000_Flora_5A110101_LOD1.asset
GEN_Shallows_Kelp_000_Flora_5A110101_LOD2.asset
```

Families present: `Shallows/Kelp`, `Shallows/TubeCoral`, `Shallows/PorousRock`.

Vertex channel block of `GEN_Shallows_Kelp_000_Flora_5A110101_LOD0.asset` — read from the `m_Channels`
list:

| Channel | Dimension | Meaning |
|---|---|---|
| 0 | 3 | Position |
| 1 | 3 | Normal |
| 2 | 4 | Tangent |
| **3** | **4** | **Color — vertex colours ARE present** |
| 4 | 2 | TexCoord0 |
| 5-7 | 0 | unused |

So the `3DMODEL_FLORA_CORAL.md` §2 R/G/B/A contract has a place to live in the real asset data. Tangents
are present, which `rendering.md:253` requires for PBR normal mapping.

These are wrapped into **200 prefabs** under `Assets/_Project/Prefabs/Nature/Flora/BioForge/`. Component
census of `GEN_Shallows_Kelp_000_Flora_5A110101.prefab`: 4 `GameObject`, 4 `Transform`, 3 `MeshFilter`,
3 `MeshRenderer`, **1 `LODGroup`**, and **zero** `MonoBehaviour`, `Animator`, or colliders.

That shape is precisely what `REND_Instanced_Flora_Physics.txt:21` designates for the *first-choice* path:
"Flora that exists as authored GameObjects with `MeshRenderer` components uses Unity 6 GPU Resident Drawer
first."

**8 of those 200 prefabs are already referenced in `02_HECTON_WORLD.unity`** (binary-aware GUID scan). So a
small amount of real generated flora is already placed in the world scene.

### 2.5 The offline forge

`Tools/Blender/` exists: `BLENDER_API_TRAPS.md`, `README.md`, `generators/` (`kelp.py`,
`coral_branching.py`, `flora_capstem.py`, `rock.py`), `h8forge/` (`export_unity.py`, `law.py`, `mesh_ops.py`,
`preview.py`, `validate.py`, `vertexcolor.py`, `blackbox.py`).

`Tools/Blender/h8forge/export_unity.py:5` — "it writes one FBX per package plus a JSON manifest".
`:73` `MANIFEST_SCHEMA = "h8forge.manifest/1"`. `:1129` `bpy.ops.export_scene.fbx(...)`. `:1218`
`export_lod_group(...)`, with LOD-ordering validation at `:1272-1288`.

`Tools/Blender/h8forge/vertexcolor.py:1-24` implements the flora bible contract faithfully, and explains
why the pipeline is in Blender at all:

> `R = water-current sway amplitude. Anchor/root = 0. Rigid mineralized coral = 0..32.`
> `G = bioluminescence mask or phase. Non-emissive tissue = 0.`
> `B = baked AO [...] The B channel is the reason this pipeline runs in Blender at all.`
> `Order of operations is load-bearing: bpy.ops.object.bake writes ALL channels [...] Baking last, or`
> `into the final attribute, silently destroys the sway gradient`

Ingestion exists: `Assets/_Project/Scripts/Editor/HectonFBXPostprocessor.cs` and
`Assets/_Project/Tests/Editor/ForgeFbxImportCarveOutEditTests.cs` both reference `h8forge.manifest`.
`export_unity.py:1409-1412` forces `optimizeMeshVertices: not vat` / `meshOptimizationFlags: "PolygonOrder"`
for VAT families — i.e. it already protects vertex order so a future VAT can index by vertex id.

**But:** `export_unity.py:1543` — "Exported with `bake_anim=False`; there is no animation data." The forge
deliberately exports no animation, and no forge-produced flora FBX has landed in `Assets/` yet (the 16 FBX
under `Assets/_Project` are vendor/Meshy/rock content). The 600 BioForge meshes came from a separate C#
generator route, not from this Blender forge.

### 2.6 Third-party contamination

`Assets/GPUInstancer/Scripts/Core/...` is present. Per `AGENTS.md:164` vendor presence is contamination,
not approval. `Assets/_Project/Art/Shaders/Hecton_KelpMaster_GPUI.shader` exists as a GPUI variant, which
suggests some intent to use it. **Not resolved by this audit** — flagged as an unknown in §9.

---

## 3. Question 2 — What the current flora scatter actually does at runtime

**Verdict against `[RULE] Zero-GC Scatter & Animation Protocol`: the scatter code PASSES the letter of the
clause. It fails on liveness, not on legality.**

There is no runtime `Instantiate` and no `Animator` anywhere in the flora/kelp/coral scatter path.

The one place that creates GameObjects, `WorldProceduralScatterDirector.cs` method `CreateScatterInstance`
(definition at `:8341`), is fenced. Verbatim:

```
8357            if (TryResolveCachedObjectPool(out pool))
8358            {
8359                instance = pool.Spawn(prefab, runtimePosition, placement.Rotation, !Application.isPlaying);
8367                if (instance == null)
8368                {
8369                    if (Application.isPlaying)
8370                        return null;
8371
8372                    instance = Instantiate(prefab, runtimePosition, placement.Rotation, parent);
8373                }
8377                if (Application.isPlaying)
8378                    return null;
8379
8380                instance = new GameObject();
```

Both GameObject-creating calls sit behind `if (Application.isPlaying) return null;` (`:8369-8370` and
`:8377-8378`). They are **edit-time bake/preview only**, which is not a hot-path violation. The sole runtime
route is the pool at `:8359`, with matching `pool.Despawn` at `:8397`.

`Animator`: a strict search for `\bAnimator\b`, `AddComponent<Animator>`, `GetComponent<Animator>`,
`[RequireComponent(typeof(Animator))]`, `animator.Play`, `animator.SetFloat` across
`Assets/_Project/Scripts/World/`, all `WorldProceduralScatter*.cs` partials, and
`Assets/_Project/Scripts/Rendering/Scatter/` returned **zero hits**. The 35 proxy prefabs and the 200
BioForge prefabs contain no `Animator` either. Motion is shader-driven.

Cold, one-shot owner roots (not per-instance, legal):
```
WorldProceduralScatterDirector.cs:4488   serviceTransform = new GameObject("__GENERATIVE_GEOLOGY_SERVICE").transform;
WorldProceduralScatterDirector.cs:11733  root = new GameObject(ScatterRootName);
FloraInteractionManager.cs:2718          GameObject sedimentObject = new GameObject("__VegetationSedimentBursts");
```

### 3.1 The actual runtime failure — a disabled component

`WorldProceduralScatterDirector` is present exactly once in `02_HECTON_WORLD`, on `[MANAGERS]/WorldGen`,
with the **GameObject active and the component disabled**. Every registration it owns runs from `OnEnable`
(`:757-777`), and Unity never calls `OnEnable` on a disabled component. Source, quoting
`Assets/_Project/Scripts/Editor/Diagnostics/H8_PlacementOwnerEnabledAudit.cs:15-24`:

> `/// WHY THIS EXISTS. A scene census run on 2026-07-27 found WorldProceduralScatterDirector present`
> `/// exactly once in 02_HECTON_WORLD, on [MANAGERS]/WorldGen, with the GameObject ACTIVE and the`
> `/// COMPONENT DISABLED. [...] So it registers nothing, ticks nothing and places nothing,`
> `/// while reading as completely correct in code review. Nothing in the project sets its .enabled`
> `/// to true at runtime, and the authoring tool that builds this stack`
> `/// (Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs:120 and :685-728) resolves`
> `/// the component with GetOrAddComponent and rewrites its serialized fields but never touches`
> `/// m_Enabled, so re-running that tool cannot repair a component that is already there and off.`

Consequence chain, all read: the three registrations at `:1748-1750`
(`TryRegisterUpdatable` / `TryRegisterSlowTickable` / `TryRegisterLateFrameTickable`, all
`PriorityLayer.Environment`) never happen → `Tick` (`:695`), `SlowTick` (`:1066`) and `LateFrameTick`
(`:1148`) never run → `CreateScatterInstance` (`:8341`) is never reached at runtime.

The system does self-report: `Awake` (`:721`) calls `ReportInertPlacementOwner` (`:751`, defined `:924`),
which emits a `LogError`, and `Awake` *does* fire on a disabled component. **Cheapest available
confirmation without taking the Unity slot: grep a recent player/editor log for that marker.** Not done
here.

### 3.2 MaterialPropertyBlock

Worth flagging against `AGENTS.md:275` (`[FORBID] MaterialPropertyBlock on standard SRP-batched geometry`)
and `REND_GPU_Sovereignty.txt:29`:

```
GpuScatterLodManager.cs:525   _materialProperties = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - per-draw indirect flora shader state - owner: GpuScatterLodManager
HectonIndirectVegetationRenderer.cs:466-472   private MaterialPropertyBlock _nearIndirectProperties; ... _motionFarIndirectProperties;
```

These are allocated once cold and feed `RenderParams.matProps` for indirect draws, which is the documented
Unity route for `RenderMeshIndirect` and is not SRP-batched geometry. Assessed **compliant**, but it is the
exact coupling that blocks a future migration to true BRG, as `:2660` warns.

### 3.3 Culling bounds versus shader motion

`PROCEDURAL_ASSET_PIPELINE.md:130` — verified verbatim:

> `Bounds must be conservative and finite. Animated shader sway, fauna appendage motion, projected wetness,`
> `and emissive pulses must fit inside runtime culling bounds. A beautiful mesh that disappears because`
> `bounds ignored shader motion is a failed asset.`

The indirect renderer passes a single whole-field `drawBounds` to every pass
(`HectonIndirectVegetationRenderer.cs:3111-3129`, set into `RenderParams.worldBounds` at `:3152`), built at
`:1612` with `Mathf.Max(_boundsSize.y, 32f)` — a 32 m vertical floor. Because the bound covers the entire
field rather than each instance, per-instance sway cannot pop an instance out. This is structurally correct
for `RenderMeshIndirect`, and it is a real advantage the current route has over a naive per-instance BRG
port, where each instance's AABB *would* need explicit sway inflation.

---

## 4. Question 3 — Is there a VAT baker?

**For flora — kelp and coral, the subject of this audit — NO. None. The mandated animation route has no
implementation whatsoever.** Flora animates entirely through analytic shader sway. No position texture is
baked, stored, or sampled for any plant in this project.

For **fauna swarms** a baker exists, has never been run, and would not work if it were:

`Assets/_Project/Editor/Generators/Fauna/AbyssalAnatomyStudio1610.cs`

- `:291-338` `BakeSwarmVatJob1610 : IJobParallelFor`, `[BurstCompile]`, correct VAT layout
  (`vertexIndex = index % VertexCount`, `frameIndex = index / VertexCount`).
- `:318-325` the payload is an **analytic sine swim wave**, not a bake of any `AnimationClip`. There is no
  `SkinnedMeshRenderer.BakeMesh` and no clip sampling in the file.
- `:366` `VatOutputRoot = "Assets/_Project/Art/Generated/Fauna/VAT1610"` — **directory does not exist.**
- `:868-873` creates one `RGBAFloat` `Texture2D` named `GEN_FaunaVAT1610_*_Position`; disk write is
  `AssetDatabase.CreateAsset(texture, assetPath)` at `:1568`.
- `:1807-1812` `[MenuItem("Hecton8/Fauna/Abyssal Anatomy Studio 1610")]` — human-reachable.
- **Never produced output.** `Assets/_Project/Art/Generated/Fauna` does not exist;
  `Assets/_Project/Art/Generated` contains only `Flora/` and `ProductFace/`. No `*FaunaVAT*` or `GEN_*VAT*`
  file anywhere. All 29 `.exr` in the repo are `ReflectionProbe-*` or shadergraph sample files.

Two defects that would make a successful run silently useless:

1. **Property-name mismatch.** `:1583` sets `"_VATPositionTex"`, `:1584-1585` set `"_H8VatPositionTex"`.
   Neither name exists in any shader in the repo. The consumer shader declares `_VatPositionTex`. Every set
   is guarded by `material.HasProperty(...)` (`:1580-1595`), so the texture is dropped with **no error**.
   `Assets/_Project/Editor/Assembly/FaunaPrefabFactory.cs:33-49` maintains arrays of five candidate alias
   names each — standing evidence the naming was never settled.
2. **Encoding contract mismatch.** The baker writes a **delta offset** (`:325`). The consumer
   `Assets/_Project/Scripts/BoidFishInstanced.shader:504` does
   `localPos += (vatPosition - localPos) * aggressiveAmplitudeScale`, i.e. treats the sample as an
   **absolute object-space position**. Feeding this baker's output to that shader collapses the mesh toward
   the origin.

The consumer side is complete and starved: `BoidFishInstanced.shader:58-68` declares the full property set,
`:225-228` the texture/sampler, `:379-393` `ResolveVatFrameUv` / `SampleVatPosition` / `SampleVatNormal`,
`:493` a correct `vertexID`-indexed read, `:496-503` two-frame lerp with wraparound. `:488` gates on
`_VatEnabled > 0.5`, and `:58` defaults `_VatEnabled` to 0, so the branch is dead by default and the
procedural tail-wag at `:513-522` runs instead. The only prefab binding in the repo,
`Assets/_Project/Prefabs/Ocean_Crest.prefab:617-618`, has `boidVatPositionTexture: {fileID: 0}` and
`boidVatNormalTexture: {fileID: 0}` — both null.

A second real baker is also unrun: `Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs`,
`:78` `OutputRoot = "Assets/_Project/BakedGeometry/Impostors"` — that directory does not exist either
(`BakedGeometry` contains only `Geology/`).

Authoring containers exist with nothing in them: `ProceduralFamily_Flora.cs:14-38` defines a
`VatDescriptor` struct, but **zero** `ProceduralFamily_*.asset` under
`Assets/_Project/Data/World/ProceduralFamilies/` contains a `positionTexture` line.

Six authority documents mandate this route with no implementation behind it: `AGENTS.md:136`,
`animation.md:93-101`, `ai.md:165-179`, `3dmodel.md:210`, `3DMODEL_FAUNA.md` (multiple), and
`.agents-skills/REND_GPU_Driven_Animation_VAT.txt` in whole. The mandate never names a baker; its only bake
reference (`:158`) assumes an external Houdini/Blender tool that does not exist here.

### 4.1 A bible/shader contradiction that must be resolved before any VAT work

`3DMODEL_FLORA_CORAL.md:24` mandates `R = water-current sway amplitude`, with the physical-leverage formula
at `:29`: `sway = saturate(distanceFromAnchor / maxFlexibleLength) ^ stiffnessExponent`.

The live shader does **not** do this. `Assets/_Project/Art/Shaders/Hecton_KelpMaster.shader`:

```
224                float vertexSeed = isfinite((float)vertexColor.r) ? (float)vertexColor.r : 0.0;
225                float phaseSeed = dot(aupPos.xz, float2(0.173, -0.131)) * swayPhaseScale + aupHash * 6.283185307 + vertexSeed * 2.1;
236                half heightMask = isfinite((float)uv.y) ? saturate(uv.y) : 0.0h;
246                positionOS.xz += normalOS.xz * (swayWave * swayAmplitude * heightMask);
```

Vertex colour **R is a phase seed** (`:224-225`), not an amplitude. The amplitude envelope is `uv.y`
(`:236`, `:246`) times the `_SwayAmplitude` material property (`:53`, default `0.08`). In the fragment
stage `:493-495` read `color.r` as a tint mask, `.g` as moisture, `.b` as age, and `:569` `.a` as vertex AO
— a **fourth** independent interpretation of the same channels.

`Assets/_Project/Art/Shaders/Hecton_IndirectVegetation.shader:1509` does the same:
`float heightMask = saturate(input.uv.y);` → `:1515` bend from a **per-instance `StructuredBuffer` field**
(`:163`), not a vertex channel. `input.color` is read only behind the
`_HectonFloraVertexColorDebug` visualiser (`:188`, used `:1820`/`:1850`).

The mandate agrees with the shader, not the bible: `REND_Instanced_Flora_Physics.txt` §III.C step 3 uses
`influence = uv.y * uv.y`.

Note also that the mandate's named shader bindings largely do not exist. Present: `_MarineSnowFlowField`
(`Hecton_IndirectVegetationShadow.shader:87`), `_SwayAmplitude`. **Absent from every live shader:**
`_FloraSwayFreq`, `_FloraSwayAmplitude`, `_FlowBendScale`, `_MaxBendAngle`, `_PlayerPushStrength`,
`_MaxDisplacement`. The project uses `_SwaySpeed`, `_SwayFrequency`, `_SwayPhaseScale`,
`_PropWashDisplacement`, `_KelpCurrentAmplitude`, `_HectonFloraSwayDisplacementField` instead.

**This must be decided by the owner before implementation, and one of the three documents must be fixed.**
`uv.y` is only meaningful for kelp blades with the lengthwise UVs of `3DMODEL_FLORA_CORAL.md:73`; it is
wrong for branching coral, where distance-from-anchor and UV V are unrelated. My recommendation is that
vertex colour R becomes the amplitude (bible wins, because it is topology-independent and the forge already
bakes it at `vertexcolor.py:6`) and the phase seed moves to a per-instance buffer field, which the indirect
shader already has infrastructure for. But that is a recommendation, not a decision I own.

### 4.2 The 35 primitive proxy prefabs — reject

Separate from the 200 good BioForge prefabs, `Assets/_Project/Data/Flora/GeneratedProxies/Prefabs/`
contains 35 `PFB_FloraProxy_*.prefab` (dated Apr 30) plus 4 materials. **All 35 reference only Unity
built-in primitive meshes** — verified by counting non-built-in mesh GUID references across all 35 files:
zero. The mesh IDs used are `10202` (Cube, 74 uses), `10206` (Cylinder, 29), `10207` (Sphere, 29), against
built-in GUID `0000000000000000e000000000000000`. Child GameObjects are literally named "Cube", "Cube",
"Cylinder".

These violate:
- `3DMODEL_FLORA_CORAL.md:14` — "Flora and coral are not primitive cylinders, spheres, ribbons, or cones."
- `3DMODEL_FLORA_CORAL.md:137` rejection gate — vertex colour R/G/B semantics: built-in primitives have
  **no vertex colour channel at all**, so the entire §2 contract is absent from the data.
- `3DMODEL_FLORA_CORAL.md:110` — "Default flora/coral collision is none." `PFB_FloraProxy_CathedralKelp`
  carries 4 `CapsuleCollider` + 1 `SphereCollider`.
- `AGENTS.md:169-170` naming — generated prefabs are `GEN_*`; these are `PFB_*`.

Zero of them are referenced by any production scene. They are wired into
`Assets/_Project/Data/World/FloraTemplates/FloraDataTemplate_*.asset` and produced by
`Assets/_Project/Scripts/Editor/FloraFoundationAuthoring.cs:612`. **They should not be scattered.** The 200
BioForge prefabs supersede them.

---

## 5. Question 4 — The Design

### 5.1 Standing constraint: do not invent a new layout

`.agents-skills/REND_Instanced_Flora_Physics.txt` already specifies this system in full — SoA layout (§II.A),
the 48-byte `FloraInstance` GPU struct (§II.B), the state bitmask (§II.B), the per-chunk buffer set (§II.C),
the zero-GC ring handoff (§II.D), the cull kernel (§III.A), the indirect-args layout (§III.B), the bending
math (§III.C), player displacement (§III.D), LOD distance math (§III.E), MX350 VRAM budget (§IV.A),
time-slicing (§IV.B), the four interface contracts (§V.A), and a nine-case failure catalogue (§V.B).

**The design deliverable here is therefore not a new architecture. It is a reconciliation** between that
mandate, the code that already exists, and the asset data that already exists. Anything I invented instead
would be a fourth competing spec.

### 5.2 Instance buffer layout

Use the mandate's struct verbatim (`REND_Instanced_Flora_Physics.txt` §II.B), which already satisfies
`AGENTS.md:285` and `DATA_Runtime_Struct_Layout_ARM64.txt`:

```
struct FloraInstance          // 48 B, 16-byte aligned
{
    float3 worldPos;          // 12 B  offset 0
    float  boundRadius;       //  4 B  offset 12
    float4 rotation;          // 16 B  offset 16   quaternion
    uint   packedData;        //  4 B  offset 32   bits[0:7]=speciesID, [8:11]=LODhint, [12:15]=stateMask, [16:31]=reserved
    float  phaseOffset;       //  4 B  offset 36
    float  pad;               //  4 B  offset 40   explicit pad
}                             // + 4 B tail pad to 48
```

Compliance check against `DATA_Runtime_Struct_Layout_ARM64.txt`: unmanaged ✓; no runtime `bool` ✓ (state is
bitfield in `packedData`); largest-to-smallest ordering ✓; explicit named padding ✓; total a multiple of 8
✓; `float4` lane alignment for vectorized shader reads ✓ (§ARM64 law rule 5). **The required byte-offset
self-audit must be produced at implementation time against
`UnsafeUtility.SizeOf<FloraInstance>() == 48`** — I have not compiled it, so the offsets above are read from
the mandate, not measured.

One deviation to record: the mandate's `float4 rotation` costs 16 B where
`REND_GPU_Driven_Animation_VAT.txt` §1.1 compresses fauna rotation to a single `uint` (smallest-three
10:10:10:2). Flora is anchored and mostly yaw-only, so a `uint` yaw + `uint` tilt would free 8 B. **Do not
take that optimisation in step one** — matching the mandate exactly is worth more than 8 bytes, and the
mandate's own VRAM budget (§IV.A: ~3 MB for 4096 × 48 B × 16 chunks) already fits the MX350 ceiling.

### 5.3 Batch ownership

Per `REND_GPU_Sovereignty.txt:50-64`, exactly one path per object family, and **no mixing**:

| Family | Path | Owner |
|---|---|---|
| The 200 `GEN_*` BioForge `MeshRenderer`+`LODGroup` prefabs | **GPU Resident Drawer** (Unity owns the BRG) | Unity; scene authoring is the only first-party work |
| Future pure-data procedural scatter with no stable GameObject | Manual BRG + compute cull | `HectonIndirectVegetationRenderer` |
| HLOD impostor cards | GPU Resident Drawer where `MeshRenderer`-owned | `ImpostorSystem` |

This is the key architectural call: **the flora we actually have is GameObject flora, so it belongs to GRD,
and GRD is Unity's own BRG.** That satisfies `AGENTS.md:136` without writing a batch owner at all.

### 5.4 GlobalQualityWeight with hysteresis

Verified accessor — do not guess this:

```
Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs:309
    public static float GlobalQualityWeight => SanitizeQualityWeight01(_globalQualityWeight, 0f);
```

Shader globals are published as `_GlobalQualityWeight` and `_H8GlobalQualityWeight`
(`HomeostasisBrain.ScalabilityDictator.cs:233-234`).

The existing consumption precedent, to be copied rather than reinvented —
`WorldProceduralScatterDirector.cs:1128-1138`:

```
float rawQuality = HomeostasisBrain.GlobalQualityWeight;
float quality = math.saturate(math.select(rawQuality, 1f, !math.isfinite(rawQuality)));
float curve = quality * quality * (3f - 2f * quality);
return math.max(1, (int)math.round(math.lerp(LowTierBudget, UltraTierBudget, curve)));
```

Note the smoothstep `q*q*(3-2q)` and the non-finite guard. That shape satisfies
`AGENTS.md:233` (continuous, no binary switch) and should be reused for both density and LOD distance.

The hysteresis precedent — `Assets/_Project/Scripts/World/SargassumCutManager.cs`:

```
32     private const float DamageVolumeQualityHysteresis = 0.08f;
929        if (_resourceQualityWeight >= 0f &&
930            Mathf.Abs(qualityWeight - _resourceQualityWeight) < DamageVolumeQualityHysteresis)
931            return;
934        _resourceQualityWeight = qualityWeight;
```

A `0.08` deadband on the weight itself, latched against the last *applied* value. Reuse this constant and
this shape for reallocation-triggering changes (density capacity, buffer resize).

Separately, `AGENTS.md:241` requires a **3-5 m or 2-3 s** band for LOD/distance switches specifically. The
weight deadband above does not satisfy that; LOD band edges need their own distance hysteresis, and
`REND_Instanced_Flora_Physics.txt` §III.E has none — it compares bare squared distances at `:317-324`.
**That is a gap in the mandate**, and the implementation must add a distance deadband (recommend 4 m, mid
of the permitted band) rather than copy §III.E literally.

`ecosystem.md:58` bounds what the weight may touch: "visible population density, secondary swarm
presentation, flora sway richness [...] It must not change biomass truth, spawn identity, save state,
predator rules, or biome ownership." So density scaling must be a **presentation cull of an
already-deterministic placement set**, never a change to the placement seed. `3DMODEL_FLORA_CORAL.md:131`
says the same for harvest/root/collision identity.

### 5.5 Sway

Drive it in-shader from the baked channel, never per-vertex on the CPU —
`3DMODEL_FLORA_CORAL.md:16`: "Runtime scripts must not calculate mesh deformation weights."

The existing `Hecton_KelpMaster.shader` already does GPU sway with a three-octave wave (`:229-232`),
`GlobalQualityWeight`-scaled amplitude (`:242-245`), AUP-safe world position (`:221-222`), and
`isfinite` guards throughout. It needs **one change**, pending the §4.1 owner decision: move the amplitude
source from `uv.y` to `vertexColor.r` and relocate the phase seed to a per-instance buffer field.

No VAT is required for flora sway. `animation.md:95-97` scopes VAT to "repeated non-interactive motion" and
explicitly lists "flora sway" alongside shader deformation as alternatives —
`Premium Approximation` (`AGENTS.md:249`) requires checking the deterministic shader approximation
**first**, and for anchored plants an analytic bend is both cheaper and more correct than a baked
position texture. **VAT is genuinely required only for the fauna/fish lane**, where the deformation is
skeletal and not expressible as an anchored bend.

### 5.6 Culling bounds

Keep the whole-field `worldBounds` approach already in `HectonIndirectVegetationRenderer.cs:1612`/`:3152`
for any manual-BRG family. For the GRD family, Unity computes bounds from the `MeshRenderer`, so the
requirement lands on the **asset**: per `PROCEDURAL_ASSET_PIPELINE.md:130` and `3dmodel.md:226` ("Mesh
bounds are conservative and finite so GPU culling does not pop silhouettes"), the generator must write mesh
bounds inflated by the maximum sway displacement. With `_SwayAmplitude` capped at `0.5`
(`Hecton_KelpMaster.shader:53` `Range(0, 0.5)`) plus flow bend, a conservative inflation of the source
mesh XZ extents is required, and `3dmodel.md:88` already puts bounds ownership on the generator:
"A generator owns normals, tangents, UVs, and bounds because it owns the geometry."
**Unverified: whether the 600 BioForge meshes were written with sway-inflated bounds.** This is a concrete
check to run.

---

## 6. Global Authority Route Card

Per `AGENTS.md:225`. Template: `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`.

```text
Route ID: FLORA_SCATTER_FIELD_RESIDENCY
Date: 2026-07-28
Owner: HectonIndirectVegetationRenderer
Owner domain: World ecology scatter / rendering
Owning file/system: Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs
First 20 Minutes moment: First exit / World load - photic shallow flora field
  (FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md:76)

Problem: 600 generated GEN_* flora meshes and 200 LODGroup prefabs exist on disk. Nothing scatters
  them at runtime. The GameObject placement owner is a disabled component in 02_HECTON_WORLD and the
  indirect renderers have no scene binding.

Why owner-local data is insufficient: chunk residency crosses the streaming pager, the AUP floating
  origin, and the quality dictator. Placement must survive origin shift and chunk unload, so instance
  buffers must be visible to the origin-shift listener and the chunk-unload disposal route.
Why direct caller/owner interface is insufficient: it is NOT insufficient for step one. See
  disposition - step one needs NO new global route at all.

Instrument:
  [x] GlobalRegistry cold service/interface   - existing IWorldGenService slot, already used
  [ ] SignalBus<T> first-party broadcast
  [ ] GlobalSignals bridge/direct queue
  [ ] HectonEventBus mod/API/cold event
  [x] GlobalDataVault / IDataVault            - existing; H8Memory.Allocate with VaultOwnerSystemId
                                                already used at HectonIndirectVegetationRenderer.cs:2686
  [ ] Black-box/telemetry route               - required before GREEN, absent today

Producer/consumer phase: producer = chunk load (cold, off frame lane); consumer = ILateFrameTickable
  for draw submission, ISlowTickable for LOD/density re-evaluation. Registration via
  GlobalRegistry.TryRegisterLateFrameTickable / TryRegisterSlowTickable, PriorityLayer.Environment
  (pattern read at WorldProceduralScatterDirector.cs:1748-1750).
Cadence/capacity: 4096 instances/chunk, 16 active chunks (REND_Instanced_Flora_Physics.txt IV.A).
  Draw submission every frame; LOD/density on slow tick; instance upload only on chunk load or
  dirty page.
Expected max events/reads per frame: 1 draw submission per species/material group, target < 8 total
  (REND_Instanced_Flora_Physics.txt IV.D). Zero per-instance CPU touches in the frame lane.

GlobalQualityWeight behavior: HomeostasisBrain.GlobalQualityWeight
  (HomeostasisBrain.ScalabilityDictator.cs:309) scales visible density and LOD distance continuously
  via the smoothstep at WorldProceduralScatterDirector.cs:1131. Reallocation-triggering changes gated
  by a 0.08 weight deadband (SargassumCutManager.cs:32 precedent). LOD band edges additionally gated
  by a 4 m distance deadband per AGENTS.md:241. Density scaling is a presentation cull of a
  deterministic placement set; it never changes the placement seed, root/anchor identity, harvest
  identity, or save state (3DMODEL_FLORA_CORAL.md:131, ecosystem.md:58).

Accessory purity:
  [x] No Get/TryGet/Resolve/Read API publishes signals
  [x] No Get/TryGet/Resolve/Read API syncs scene state
  [x] No Get/TryGet/Resolve/Read API allocates/grows buffers
  [x] No Get/TryGet/Resolve/Read API completes jobs
  [x] No Get/TryGet/Resolve/Read API mutates global state
  [x] No Get/TryGet/Resolve/Read API searches the scene
  (Asserted against the IFloraChunkProvider / IFlowFieldSampler / IFloraPlayerInterface /
   IFloraCullDispatcher shapes in REND_Instanced_Flora_Physics.txt V.A - TryGet* returning false on a
   not-yet-loaded chunk, per AGENTS.md:281 fail-safe reservation rule. NOT yet verified against
   compiled source.)

Payload/data shape: FloraInstance, 48 B, 16-byte aligned (REND_Instanced_Flora_Physics.txt II.B)
Managed fields present: no
UnityEngine.Object fields present: no
Layout proof: PENDING - requires UnsafeUtility.SizeOf<FloraInstance>() == 48 plus the field-offset map
  required by DATA_Runtime_Struct_Layout_ARM64.txt "Required Self-Audit". Not produced by this audit.

Overflow/failure: chunk instance count > 4096 -> deterministic truncation by morton order, telemetry
  counter incremented, no reallocation in frame lane. GraphicsBuffer upload budget exceeded ->
  GraphicsBufferUploadUtility.RecordManualUploadDeferred and skip (existing behaviour,
  ScatterGPUIBackend.cs:77-81). Chunk buffer not ready -> TryGetChunkInstanceBuffer returns false,
  dispatch skipped, no null deref.
Telemetry fields: instances requested / placed / truncated, uploads deferred, active chunk count,
  visible instance count per LOD, quality weight applied, frames since last density change.
Black-box fields: PENDING - AGENTS.md:454 requires a 300-frame ring for critical runtime systems.
  No BufferID is currently allocated for flora scatter. This is a REQUIRED-BEFORE-GREEN gap.
Profiler marker: PENDING - required per performance.md "Frame Budget Law". Absent today.
GC proof required: yes, GCMonitor 0 B/frame on the shallow route.

Shutdown/disposal: strict order per REND_Instanced_Flora_Physics.txt V.D - complete outstanding
  JobHandles, release GraphicsBuffers, dispose NativeArrays, release shared flow-field buffer last.
  Must subscribe to the streaming pager unload event and Dispose per AGENTS.md:138.
  Existing precedent: ScatterGPUIBackend.Dispose (ScatterGPUIBackend.cs:133-143).
Scene unload behavior: all per-chunk buffers released; no static mutable state survives domain reload.
Stale-handle behavior: ChunkID validity mask; generation-checked Vault handles; unload only after
  dispatch completion is confirmed.
DataVault write-lock scope, if applicable:
  [x] Lock acquired immediately before owned mutation/copy/staging/schedule
  [x] Lock released in `finally` or equivalent scoped disposal in same owner phase
  [x] Lock is not held across frame boundary, await, worker sleep, UI callback, or unrelated work
  [x] Job dependency/fence is named when scheduled work touches Vault-backed memory
  (Asserted from the existing try/finally H8Memory.Release pattern at
   HectonIndirectVegetationRenderer.cs:2690-2704. NOT independently verified across all call sites.)

Rejected alternatives:
  [x] owner-local field          - insufficient: crosses streaming/origin/quality owners
  [x] cached owner interface     - SUFFICIENT for step one; this is the selected step-one answer
  [ ] existing SignalBus lane
  [x] existing Vault buffer      - selected; reuse H8Memory/VaultOwnerSystemId, no new BufferID in step one
  [ ] cold HectonEventBus hook
  [x] no global route needed     - TRUE FOR STEP ONE

Why this does not increase global monolith risk: step one adds no global surface whatsoever. It
  enables an existing Unity subsystem (GPU Resident Drawer) and places existing prefabs. The manual-BRG
  route in step three reuses GlobalRegistry slots, tick registrations, and Vault ownership that the
  renderer already declares; it introduces one telemetry ring and one profiler marker, both of which
  are required by existing law rather than new surface.
H-Phi impact expected: none claimed. Per GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md:295, H-Phi cannot
  convert a disposition.

Proof required before GREEN:
  1. Unity import with no route-blocking Console errors.
  2. Frame Debugger / Rendering Statistics: GPU Resident Drawer active path confirmed; SetPass and
     batch count before/after (REND_GPU_Sovereignty.txt:70-76).
  3. Play Mode capture of the shallow route showing a populated kelp field.
  4. GCMonitor 0 B/frame on that route.
  5. Memory/VRAM snapshot against the MX350 1800 MB ceiling.
  6. Compact-tier (URP_Low + Mobile_Renderer) capture proving readability, not just PC-High.
  7. Visual Reference Parity Gate against the mandatory reference folder - lead-owned, per
     AGENTS.md:140 and CLAUDE.md "Player-visible taste standard".
  8. Struct byte-offset self-audit if step three is reached.
  9. Telemetry ring + profiler marker present if step three is reached.

Reviewer: unassigned
Review disposition: YELLOW (see below)
Status: PROPOSED
```

### 6.1 Checklist Disposition

Per `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md:69-83`.

```text
Global authority review:
Result: YELLOW
Route ID: FLORA_SCATTER_FIELD_RESIDENCY
Owner: HectonIndirectVegetationRenderer
Instrument: existing GlobalRegistry cold slot + existing GlobalDataVault ownership; no new surface in step one
Producer/consumer phase: chunk-load (cold) -> ILateFrameTickable draw, ISlowTickable LOD/density
Cadence/capacity: 4096 instances x 16 chunks; <8 draw submissions/frame
Overflow/failure: morton-order truncation + telemetry; upload deferral; TryGet false on unready chunk
Shutdown/disposal: jobs completed, buffers released, arrays disposed, flow field last; pager-unload subscribed
Proof required before GREEN: the nine items above
Review disposition: YELLOW
Reason: The concept is valid and the narrowest correct instrument is selected - step one needs no new
  global route at all, which is the strongest possible answer to the "owner-local first" test. It is
  not GREEN because the checklist's own Immediate Rejection list (:107-137) names two fields this
  route cannot yet fill: "no telemetry or black-box fields" and no profiler marker. AGENTS.md:454
  requires a 300-frame ring for critical runtime systems and no BufferID exists for flora scatter.
  Per :77, a proof plan alone is YELLOW, never GREEN, and every runtime claim in this audit is
  static-read only.
Required fixes before GREEN:
  1. Allocate a flora-scatter telemetry BufferID + 300-frame black-box ring with a named dump target.
  2. Add the named profiler marker required by performance.md "Frame Budget Law".
  3. Resolve the vertex-colour-R contradiction in section 4.1 - owner decision, then fix whichever of
     3DMODEL_FLORA_CORAL.md, REND_Instanced_Flora_Physics.txt, or the shaders is wrong. Three
     documents currently disagree and no implementation should proceed on top of that.
  4. Produce the FloraInstance byte-offset self-audit if step three is reached.
Proof still missing: all nine items. No Unity, profiler, GC, or visual proof was produced.
Reviewer: unassigned - this is a proposal, not an approved route
Date: 2026-07-28
```

---

## 7. Question 5 — The Cheapest Correct First Step

**Do not write a VAT baker. Do not port anything to BRG. Do not touch the 7123-line renderer.**

The measured project state settings-side:

| Setting | File:line | Value read | Required by |
|---|---|---|---|
| GPU Resident Drawer | `Assets/_Project/Data/URP_Medium (PC_RPAsset).asset:86` | `m_GPUResidentDrawerMode: 0` | `REND_GPU_Sovereignty.txt:12`, `REND_Instanced_Flora_Physics.txt:21` |
| ” | `URP_Low (PC_RPAsset).asset:86`, `URP_High (PC_RPAsset).asset:86`, `Mobile_RPAsset.asset:86`, `URP_Quest_VR.asset:86` | `0` on **all five** | ” |
| GRD occlusion culling | same files, `:88` | `m_GPUResidentDrawerEnableOcclusionCullingInCameras: 0` | `REND_GPU_Driven_Animation_VAT.txt:119` |
| Rendering path, PC | `Assets/_Project/Data/PC_Renderer.asset:280`, `PC_High_Renderer.asset:328` | `m_RenderingMode: 2` | Forward+ ✓ prerequisite MET on PC |
| Rendering path, compact | `Assets/_Project/Data/Mobile_Renderer.asset:193` | `m_RenderingMode: 0` | Forward+ ✗ **NOT met on the compact lane** |
| BRG variants | `ProjectSettings/GraphicsSettings.asset:50` | `m_BrgStripping: 0` (Keep All) | `REND_GPU_Driven_Animation_VAT.txt:118` ✓ MET |
| Static batching | `ProjectSettings/ProjectSettings.asset:529` | `m_StaticBatching: 1` | `REND_GPU_Driven_Animation_VAT.txt:118` and `REND_Instanced_Flora_Physics.txt:27` require **OFF** ✗ |

(`m_GPUResidentDrawerMode` and `m_RenderingMode` numeric meanings are read as literals; the mapping
`0 = Disabled` / `2 = ForwardPlus` is from Unity 6 URP enum ordering and is **inferred**, not read from a
project file. Confirm in the inspector before acting.)

### The step

1. **Re-enable the placement owner.** `WorldProceduralScatterDirector` is already in `02_HECTON_WORLD` with
   `m_Enabled: 0`. The repair tool already exists and is a deliberate human `MenuItem`:
   `H8_PlacementOwnerEnabledAudit` (`Assets/_Project/Scripts/Editor/Diagnostics/H8_PlacementOwnerEnabledAudit.cs`),
   Undo-recorded, and it never saves the scene itself (`:35-40`). One click plus Ctrl+S.
2. **Point it at the 200 `GEN_*` BioForge prefabs, not the 35 primitive proxies.** 8 of 200 are already in
   the scene, so the binding shape is proven.
3. **Enable GPU Resident Drawer on the PC URP assets** and **disable Static Batching**.
4. **Capture the proof.** Frame Debugger for the GRD path, Rendering Statistics SetPass/batch delta,
   GCMonitor, Play Mode screenshot of the shallow route, plus the compact-tier capture.

That is a settings change, a scene authoring change, and a capture run. It writes **no new runtime code**,
adds **no global surface**, and lands on the path the law names as first choice for `MeshRenderer` flora.

**Blocked on owner approval, not on engineering.** `AGENTS.md:334` forbids changing project settings,
Quality, or URP assets "without explicit instruction or narrow route proof", and steps 1 and 3 are exactly
that. This audit is the narrow route proof; the instruction is the user's to give.

### What it defers

- The VAT baker for flora — correctly, and possibly permanently. §5.5: anchored plants do not need it.
- Manual BRG migration and the `MetadataValue` port described at
  `HectonIndirectVegetationRenderer.cs:2669-2672`.
- The `FloraInstance` 48-byte buffer and its cull compute — not needed while flora is GameObject-owned.
- Per-instance sway from vertex colour R, pending the §4.1 owner decision.
- The compute-shader flow-field bend of `REND_Instanced_Flora_Physics.txt` §III.C.
- Impostor HLOD baking (`HectonOctahedralImpostorBaker.cs`, never run).
- The compact-lane Forward+ decision. `Mobile_Renderer.asset:193` is plain Forward, so GRD cannot run there
  and the compact lane keeps the current path until that is resolved. **This is a genuine scalability hole,
  not a deferral of convenience** — `AGENTS.md:237` forbids low-vs-ultra dichotomies, and step one
  currently improves only the PC lane.

### Is the existing scatter good enough for a first pass?

**Yes, on the code axis. Emphatically no on the data-and-wiring axis.**

The scatter code is legally clean against the Zero-GC clause: no runtime `Instantiate`, no `Animator`,
pooled spawns, cold-allocated MPB, Burst culling, double-buffered uploads, AUP-correct matrices, finite
guards throughout. Someone did this properly. Rewriting it toward manual BRG now would be
`AGENTS.md:100`-style polish before base beauty: the frame has no flora at all, and the reason is a disabled
checkbox and an unbound component, not the draw call.

The honest conclusion is that **the mandated route can wait**, and the thing that cannot wait is turning
the existing route on.

---

## 8. Cost

Static estimate, no measurement. Ranges assume one engineer plus the Unity slot.

| Stage | Scope | Cost |
|---|---|---|
| Step one (§7) | Enable placement owner, enable GRD, disable static batching, place `GEN_*` prefabs, capture proof | **1-2 days**, most of it the proof package and the compact-tier capture |
| Resolve §4.1 contradiction | Owner decision + fix one of three documents + shader amplitude change + reimport | **1-2 days** after the decision; the decision itself is a blocker of unknown duration |
| Telemetry + profiler marker (YELLOW→GREEN) | BufferID, 300-frame ring, dump target, marker | **2-3 days** |
| Compact-lane Forward+ | Evaluate Forward+ on `Mobile_Renderer`, re-cost the MX350 budget, recapture | **3-5 days**, and it may fail on the 2 GB VRAM ceiling |
| Manual BRG migration | Port seven `MaterialPropertyBlock` binding groups to `MetadataValue` + batch buffer per `:2660-2672`, `FloraInstance` buffer, cull compute, offset self-audit, failure catalogue | **3-5 weeks.** This is the real number. Do not let a two-day sketch stand in for it. |
| Flora VAT baker | Not recommended — see §5.5 | n/a |
| Fauna VAT baker repair | Fix the two defects in §4, settle property naming, produce first artifact, bind to `ProceduralFamily_Fauna` | **1-2 weeks**, separate lane, separate audit |

The manual-BRG number is large because the blocker is not the BRG call — it is that the renderer publishes
**every** binding through `MaterialPropertyBlock`, and BRG consumes none of them. That is a rewrite of the
renderer's entire data-publication layer, and `:2671-2672` is right that deleting or waking that path is an
architecture decision, not a static-read one.

---

## 9. Blockers And Unknowns

**Blockers (need an owner decision or a resource I do not hold):**

1. Steps 1 and 3 of §7 change project settings and a production scene. `AGENTS.md:334` requires explicit
   instruction. **Not mine to take.**
2. The §4.1 vertex-colour contradiction. Three documents disagree about what channel R means. No flora
   sway implementation should be written until one is authoritative.
3. The Unity slot was held throughout this audit, so **nothing here is runtime-verified.**
4. `Mobile_Renderer.asset:193` is Forward, not Forward+, so the GRD route has no compact lane today.

**Unknowns I could not close by static read:**

1. Whether the 600 BioForge mesh bounds were written sway-inflated per `PROCEDURAL_ASSET_PIPELINE.md:130`.
   `GEN_Shallows_Kelp_000_Flora_5A110101_LOD0.asset:153-155` reads
   `m_LocalAABB: m_Center {x: 0.245, y: 9.180, z: 0.5}, m_Extent {x: 1.345, y: 9.415, z: 0.735}` — an
   ~18.8 m tall plant with 1.345 m XZ half-extent. Whether that XZ figure already includes sway headroom
   or is the raw vertex hull is **undetermined**: it needs the raw vertex extents to compare against, which
   means reading the packed `m_VertexData` blob rather than the AABB. Concrete check: sample the position
   stream, take the true XZ extent, and confirm `boundsExtent.xz - trueExtent.xz >= maxSwayDisplacement`
   where max displacement is `_SwayAmplitude` (capped `0.5`, `Hecton_KelpMaster.shader:53`) plus the flow
   bend term at `:247`.
2. Whether the vertex colours in those 600 meshes actually follow the `3DMODEL_FLORA_CORAL.md` §2 ramp, or
   are uniform/degenerate. Channel 3 exists at dimension 4; its **contents** are unverified. This is the
   classic silent-degeneracy failure — a channel present and uniformly zero looks identical to a channel
   present and correct at this level of inspection. Worth a probe.
3. Whether `Assets/GPUInstancer/` is actually called from first-party code, and what
   `Hecton_KelpMaster_GPUI.shader` is for. Vendor contamination versus sanctioned route is unresolved.
4. `GameBootstrapper`'s GUID appears in **no** scene, including the text-YAML `00_BOOTSTRAP.unity` (39
   unique GUIDs total, verified by direct `grep`). Either it is installed by a route I did not trace, or
   `00_BOOTSTRAP` is not the entry point the docs claim. **Out of scope here, but it looks wrong and
   deserves its own look.**
5. Which of the 8 in-scene BioForge prefabs are where, and whether they are on the first-20 route or parked
   in a corner. Needs the loaded scene graph, not a file scan.
6. Whether a play session is currently emitting the `ReportInertPlacementOwner` `LogError`
   (`WorldProceduralScatterDirector.cs:924`). Cheapest confirmation of §3.1 available without the Unity
   slot: grep a recent log for that marker.

---

## 10. Rejection Gates For This Route

Reject the implementation if:

- flora is scattered from the 35 `PFB_FloraProxy_*` primitive prefabs (§4.2);
- GPU Resident Drawer and manual BRG own the same object family (`REND_GPU_Sovereignty.txt:64`);
- a VAT baker is written for anchored flora before `Premium Approximation` (`AGENTS.md:249`) is tested
  against the existing analytic shader bend;
- density scaling changes the placement seed, root/anchor identity, harvest identity, or save state
  (`3DMODEL_FLORA_CORAL.md:131`, `ecosystem.md:58`);
- an LOD or density switch ships without hysteresis (`AGENTS.md:241`);
- `MaterialPropertyBlock` is added to SRP-batched flora geometry (`AGENTS.md:275`);
- static batching remains on for GRD-owned flora (`REND_Instanced_Flora_Physics.txt:27`);
- the route is claimed GREEN without the telemetry ring and profiler marker (§6.1);
- readiness is claimed from settings values or this document rather than Frame Debugger, profiler, GC, and
  compact-tier capture (`AGENTS.md:108`);
- the field is accepted on a PC-High capture alone with no compact-tier proof
  (`rendering.md:20`, `AGENTS.md:239`).

## 11. Acceptance Sentence

This route is accepted only when a generated kelp field is visibly scattered on the first-20 shallow route
through a single named draw path, animates from baked vertex data in-shader with no CPU deformation and no
`Animator`, scales density and LOD continuously from `HomeostasisBrain.GlobalQualityWeight` with
hysteresis, survives origin shift and chunk unload without leaking native memory, holds 0 B/frame under
GCMonitor, stays inside the MX350 VRAM ceiling, and beats the mandatory reference set on a compact-tier
capture — not when the architecture diagram is correct.
