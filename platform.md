# HECTON-8 Platform And Hardware Bible

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Scope: hardware tiers, platform proof ladder, MX350/i3 baseline, Steam Deck/Linux, macOS, XR, Quest/PICO, console-readiness boundaries, shader portability, native plugin risk, and platform quality scaling.

## 1. Prime Law

Platform readiness is not serialized settings, package presence, or hopeful compatibility.

HECTON-8 must look expensive on high-end hardware and remain readable on weak hardware. The project rejects two failures:

- building only for a beauty-shot PC and then discovering compact hardware cannot read the route;
- flattening the whole game to weak hardware until high-end looks lifeless.

Platform work exists to protect the same gameplay truth across devices while changing only presentation cost, cadence, density, and optional sensory detail.

## 2. Platform Truth Ownership

Platform truth is owned by current artifacts:

- build command and target;
- Unity import/Console proof;
- player build and launch proof;
- hardware/device capture;
- profiler, GC, memory, VRAM, and Frame Debugger proof;
- input, storage, shader, native plugin, and save/load proof where relevant.

`platform.md` defines what proof is needed per target. `release.md` decides whether a release claim can be made. `rendering.md`, `ui.md`, `audio.md`, `streaming.md`, and `data.md` define the domain-specific cost routes.

## 3. Proof Ladder

Platform readiness climbs in this order:

1. Windows Editor import and clean current Console.
2. Windows standalone flat player build and launch.
3. First playable route: boot, menu, world, orient, gather/salvage, use tool, repair/craft/build, hazard, save, load, return.
4. Compact PC capture: frame time, GC, memory, VRAM, hitches, readability.
5. Content payload gate: DataMonolith, Addressables, payload hashes, load proof.
6. Linux or Steam Deck: Proton/native run, 1280x800 UI, controller glyphs, storage, shader/native-plugin proof.
7. macOS: Metal shader import/compile, Apple Silicon/Intel decision, native plugin parity, signing/notarization path when needed.
8. XR package/import/provider smoke.
9. PCVR smoke: headset boot, input, UI, comfort, foveation on/off proof.
10. Android ARM64 non-XR IL2CPP smoke.
11. Quest 3 standalone smoke, then Quest 2 thermal/stress pass.
12. PICO provider/device smoke after Quest path is stable.
13. Consoles only after SDK/devkit/certification constraints are real.

Skipping the ladder is platform theater.

## 4. Hardware Tiers

Do not reduce platform support to low/high switches.

Required lanes:

- Compact: i3/MX350/2GB VRAM-class proof lane, survival readability, strict memory and draw discipline.
- Handheld UMA: Steam Deck-like memory sharing, 1280x800 UI, controller glyphs, shader stutter risk, storage latency.
- Middle PC: stable 1080p route, moderate sensory density.
- High PC: denser visuals, richer lighting/VFX, longer residency, better captures.
- Ultra PC: visual overkill with the same truth, save identity, DTO layout, and route ownership.
- XR/VR: comfort, stencil/fragment rejection, input readability, foveation proof, thermal proof.

`GlobalQualityWeight` moves continuously across these lanes. It may scale presentation. It must not change gameplay authority.

## 5. MX350 And Compact Rules

Compact proof is the anti-fraud lane.

Required:

- route readable in fog/darkness/water;
- UI readable at low resolution;
- no hot-path GC;
- texture memory within budget;
- render targets bounded;
- no unbounded volumetrics;
- no always-on particle fog;
- no runtime mesh/collider generation;
- no VRS/foveation assumption without hardware capability proof;
- shader variant count owned and stutter tested.

If Compact fails, the feature is not production-ready even if Ultra screenshots look strong.

## 6. Shader And Graphics API Portability

D3D success does not prove Vulkan, Metal, Android, XR, or console success.

Platform shader work must define:

- graphics API;
- URP/RenderGraph path;
- shader variant collection;
- warmup route;
- first-use traversal test;
- native plugin dependency;
- fallback or disable route;
- proof capture.

Forbidden:

- `Shader.WarmupAllShaders()` during gameplay;
- claiming Linux/Vulkan readiness from Windows D3D proof;
- shipping VRS/foveation without capability checks;
- creating runtime material variants that were not represented in build/warmup proof.

## 7. XR And Foveation Boundary

XR is not a free high-end mode.

XR proof requires:

- provider import/config proof;
- headset launch;
- input binding proof;
- UI readability and comfort proof;
- stencil or rejection path for expensive visor/HUD fragments where needed;
- foveation/VRS capability proof;
- thermal and frame pacing proof for standalone devices;
- motion sickness risk review.

Foveated rendering and VRS are optional High+ techniques. They are not MX350 baseline assumptions.

## 8. Native Plugin And Package Risk

Native plugins and third-party packages must have a target matrix:

- supported platforms;
- fallback path;
- load failure behavior;
- build inclusion/exclusion rule;
- memory ownership;
- thread and callback boundary;
- license/certification risk where relevant.

Core gameplay must not depend on a package that cannot fail closed on unproven platforms.

## 9. Input, Storage, And UI Per Platform

Every platform target must prove:

- primary input route;
- remapping or platform glyph route;
- save/storage path;
- UI scale and safe area;
- localization/font atlas risk;
- screenshot readability;
- quit/suspend/resume behavior where applicable.

Steam Deck and handheld targets require 1280x800 proof. XR requires per-eye/HUD comfort proof. Consoles require platform-holder certification requirements before readiness claims.

## 10. GlobalQualityWeight Scaling

Compact:

- fewer effects;
- lower texture residency;
- cheaper fog/water;
- lower particle density;
- lower AI/swarm/IK cadence;
- strong UI/audio/readability redundancy.

Middle:

- fuller world density;
- more local lights;
- richer audio layers;
- better animation contact.

High:

- higher render scale or density;
- better shadows and VFX;
- longer HLOD residency;
- richer material response.

Ultra:

- capture-grade visual overkill;
- richer near-field water/fog/material/animation detail;
- denser telemetry/debug where development build;
- no different gameplay truth.

The high end buys sensory density, not easier rules.

## 11. Proof Artifacts

Platform work must provide:

- target platform and hardware;
- build command and artifact;
- launch proof;
- Unity import/Console state;
- profiler/GC/memory/VRAM proof where runtime-facing;
- Frame Debugger or shader import proof where rendering changed;
- input/UI/storage proof;
- save/load proof for gameplay targets;
- shader warmup/stutter proof for Vulkan/Linux/handheld;
- foveation/VRS capability proof for XR claims;
- native plugin parity/fallback proof;
- explicit `PENDING VERIFICATION` for targets not run.

## 12. Rejection Gates

Reject platform claims if:

- they are based on static docs only;
- they skip the proof ladder;
- they call package presence readiness;
- they claim VRS/foveation without hardware capability proof;
- they claim Steam Deck/Linux from Windows D3D;
- Compact readability fails;
- high-end visuals change truth;
- platform-specific branches alter save identity, DTO layout, or gameplay authority without a route card.

## 13. Acceptance Sentence

Platform work is accepted only when current device artifacts prove that HECTON-8 preserves gameplay truth, readability, memory discipline, input/storage safety, shader compatibility, and continuous quality scaling on the claimed target without using platform fantasy as release proof.
