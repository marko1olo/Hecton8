# 17 Sensory Simulation And Player Channels

Status: PENDING VERIFICATION

Mandates followed:
- `AUDIO_Hrtf_Binaural_Spatialization.txt`
- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`
- `CORE_Weather_Abyssal_FlowField_Currents.txt`
- `CORE_Damage_System_Hull_Integrity_VFX_Feedback.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`

Purpose:
- Audit the player-facing sensory runtime: sound, weather, visor/HUD, sonar, stress/glitch, and the simulation channels that feed them.
- Decide whether the horror/survival fantasy is actually encoded in runtime systems or still mostly promised by documents.

## 1. Domain weight

Static snapshot:

| Domain | Files | Lines | `Instance` hits | `GlobalRegistry.` hits | Native/Burst surface | `Complete()` hits | Direct `Action` events |
|---|---:|---:|---:|---:|---:|---:|---:|
| Audio | 13 | 7,874 | 12 | 23 | very high | 6 | 1 |
| AudioLog | 4 | 707 | 17 | 4 | light | 0 | 0 |
| Atmosphere | 5 | 3,370 | 2 | 12 | light-medium | 1 | 0 |
| Visor | 19 | 8,206 | 21 | 34 | light | 0 | 5 |

Interpretation:
- The sensory stack is not one subsystem. It is a dense cluster.
- `Audio` and `Visor` alone already form heavyweight pillars comparable to entire mid-sized game features.

## 2. Audio is not decorative

Evidence:
- `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:31` is a `4.2k` line owner, not an effect helper.
- It carries a huge sample-domain scratch surface via `NativeArray<float>` and probe buffers (`328-383`, `2092-2103`).
- It schedules/finishes at least one raycast-driven sonar path (`1628`) and registers into the dispatcher (`1986-2008`).
- `HectonMusicDirector.cs:17` is another major runtime owner at `2.1k` lines, with its own environment/story coupling and mixed authority (`328`, `677-703`, `941`, `1150`, `2151+`).

What is genuinely good:
- This is a real authored-plus-procedural audio runtime.
- Hull stress, active sonar, impact clang, thruster beds, heartbeat, binaural cues, and abyssal pressure coloration are encoded in code, not just in mood docs.
- The project’s audio ambition is unusually concrete for a Unity game of this type.

What is bad:
- `PlayerCriticalProceduralAudioRenderer` is an owner-monolith by any honest standard.
- `HectonMusicDirector` still uses mixed authority and broad cross-system reads. It feels like a powerful live conductor that also knows too much about too many neighbors.
- The sensory stack gains thematic depth and pays for it with ownership sprawl.

Verdict:
- Audio implementation reality: extremely high.
- Runtime cleanliness: medium.
- The game’s mood is genuinely being implemented in engineering terms, not merely described.

## 3. Atmosphere is a real simulation bridge

Evidence:
- `HectonSurfaceWeatherDirector.cs:24` is `1.9k` lines and implements `ITickable`, `IUpdatable`, `ISlowTickable`, `ILateFrameTickable`, `IOriginShiftListener`.
- It owns a `NativeArray<SurfaceWeatherJobOutput>` (`317`, `643`) and at least one sync point (`683`).
- It binds across ocean, celestial, player, audio, and visor-adjacent systems (`499`, `529`, `542`, `563`, `1548`).

What is genuinely good:
- Surface weather is not a shader preset switch. It is a runtime director that knows about shelter, thunder, local precipitation, and cross-system presentation.
- The project really does encode atmospheric state as gameplay-adjacent runtime truth.

What is bad:
- Again, the owner is broad.
- A single weather owner touching player, audio, ocean, acoustic zone, and celestial channels is powerful but expensive to reason about.

Verdict:
- Atmosphere implementation reality: high.
- Sovereignty: medium.
- It materially supports the NASA-punk/deep-sea fantasy instead of faking it.

## 4. Visor stack is massive and central

Evidence:
- `VisorHUDController.cs:24` is `1.4k` lines, `ExecuteAlways`, and owns runtime render textures, adaptive scaling, trauma/hypoxia/frost/interference state, and shader IDs.
- It rents from `RenderTexturePool` and checks `DynamicResolutionScaler`, `VRAMMonitor`, and `RenderTextureLifecycleTracker` (`1043`, `1087-1091`, `1173-1178`).
- `SpectrumSystem.cs:109` is `975` lines and still uses singleton identity and direct static events through `SpectrumEvents` (`84-91`, `212`, `312-370`).
- `SuitHUDPresentationController.cs:15` is another active runtime HUD owner registered through `GlobalRegistry` (`751-763`).
- The folder also contains many custom URP features, not just MonoBehaviour glue.

What is genuinely good:
- This is one of the strongest proofs that the game’s player-facing identity is real.
- Sonar, visor interference, HUD projection, screen-space features, and environmental visor state all exist as concrete runtime systems.
- The stack understands hardware pressure, RT lifecycle, and adaptive behavior. That is serious engineering, not presentation scripting.

What is bad:
- The visor/spectrum path is architecturally mixed.
- `SpectrumEvents` is still direct static `Action` territory while the rest of the project simultaneously argues for queue-backed event rigor.
- `ExecuteAlways` plus runtime ownership plus pooled RT lifecycle means this stack has high regression sensitivity.

Verdict:
- Player-channel implementation reality: extremely high.
- Technical fragility: medium-high.
- This stack is one of the project’s clearest strengths and one of its most expensive maintenance zones.

## 5. Physics as a domain is weirdly absent and materially present

Evidence:
- `Assets/_Project/Scripts/Physics` currently has `0` C# files.
- Yet physics logic is visibly spread across audio, atmosphere, interaction, construction, visor, and gameplay namespaces.
- Examples:
  - sonar/raycast physics in `PlayerCriticalProceduralAudioRenderer`
  - movement/flow/ocean coupling in `HectonSurfaceWeatherDirector`
  - interaction raycast scheduling in `EquipmentInteractionHandler`
  - structural validation and habitat graph reasoning in construction internals

What this means:
- There is no clean sovereign physics module in folder reality.
- Physics exists as a distributed capability layer embedded into many owners.

Why that matters:
- This can work.
- It also means architecture maps that imply `Hecton8.Physics` as a crisp owned domain are only partially true in code reality.

Verdict:
- Physics implementation reality: distributed and real.
- Physics architectural locality: low.

## 6. Audio + visor + weather together

This is the most important synthesis from this pass.

These systems form a genuine player-sensory triangle:
- weather changes audio and surface presentation
- visor/hud responds to damage, hypoxia, and hazard channels
- audio responds to hull stress, pressure, sonar, and transport state

This is not accidental flavor.
It is already a live design language implemented in code.

That is a major strength.

The cost:
- each corner of that triangle is also broad and owner-heavy
- event discipline is inconsistent between them
- some parts are native/data-oriented, some are still static/singleton/event-heavy

## 7. Hard conclusion

If someone asked whether HECTON-8 already has a real sensory identity, the answer is yes.

Not "maybe."
Yes.

The honest warning is different:
- the sensory identity is being carried by several powerful owner-monoliths
- those owners are strong enough to impress and strong enough to become regression traps

This is a real game stack, not a fake atmosphere layer.
