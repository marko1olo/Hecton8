# SYSTEMS_CONTRACTS.md
## ENGINE SYSTEMS CONTRACTS
Status: PENDING VERIFICATION
Verification: not runtime-measured in this pass
Scope: Save, Steam, Audio, Telemetry, CI, Accessibility, Endgame

Current-state boundary, 2026-05-01:

- This file is a contracts and target-behavior document, not a proof that every listed file/class exists or is production-ready.
- Current source-backed system ownership starts from `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.
- Current save implementation authority is `SaveManager.cs` / `SaveBinaryStorage.cs`; versioning and migration requirements below remain contractual.
- Current audio service authority is `SpatialAudioManager` plus procedural audio owners; older `UnderwaterAudioProcessor.cs` naming below is a target contract unless source confirms a concrete owner.
- No line in this document is a clean-console, zero-GC, Steam, CI, or accessibility verification claim without a fresh runtime/log artifact.

These systems are developed separately from asset pipeline work.
[FORBID] Use this document as primary source for asset generation.
[FORBID] Mix this scope with flora, geology, or structure authoring.
[REQ] Cross-reference from QUALITY_GATES.md for benchmark tooling
      (BenchmarkRunner.cs) is permitted and expected.

---

## SAVE VERSIONING & MIGRATION

File: SaveVersioning.cs, SaveMigrator.cs

[REQ] Save files must include:
      uint SaveVersion, string EngineVersion, uint Checksum.
[REQ] On load: SaveVersion < CURRENT_VERSION ->
      SaveMigrator.ApplyDelta(). Never load raw data.
[REQ] Corruption recovery: .bak promoted if .sav checksum fails.
      Log to crash_telemetry.log.
[FORBID] Direct JSON overwrite without backup.
[FORBID] .tmp -> .sav rename on crash.

### Delta Save Rules
[REQ] Procedural chunks serialize ONLY:
  - Player-placed/modified structures
  - Cut/removed resources (isCut=true)
  - Dropped inventory items
  - AI state flags (droneTension, baseIntegrity)

[FORBID] Save heightmaps, scatter coordinates, or full world state.
[REQ] On load: regenerate chunk from seed -> apply delta ->
      verify terrain alignment ->
      snap player to nearest safe surface.

---

## STEAMWORKS INTEGRATION

File: SteamManager.cs, CloudSaveSync.cs

[REQ] Achievements, Cloud Saves, Telemetry. Workshop disabled.
[REQ] Cloud saves: sync on Application.quitting
      and OnApplicationPause(false).
      Conflict resolution: latest_timestamp wins.
[REQ] Wishlist tracking: SteamUserStats.GetStat("WishlistSource")
      on first launch.
[FORBID] Sync on FixedUpdate.
[FORBID] Blocking SteamAPI.RunCallbacks() in main thread.
[FORBID] Unhandled achievement unlocks.

---

## UNDERWATER AUDIO

File: UnderwaterAudioProcessor.cs, AudioMixer.mixer

[REQ] All 3D audio -> AudioMixerGroup "Underwater_Occlusion".

### DSP Chain (order is mandatory)
1. LowPassFilter
   cutoff = Lerp(20000, 400, depth/5000). Q = 1.0.
2. ConvolutionReverb
   wet/dry by proximity to hard surfaces, decay 1.2s-3.5s.
   No real-time convolution on MX350.
3. Dynamic Ducking
   critical SFX > ambient >= 12dB, attack 0.05s, release 0.3s.
4. HRTF panning: DISABLED.
   Underwater directionality is amplitude/frequency based, not phase.

[REQ] Doppler disabled underwater.
[REQ] Distance rolloff = CustomCurve (logarithmic, fast decay).
[REQ] Reverb zones: AudioReverbPreset per biome
      via SetAudioListenerReverb(). No real-time convolution.
[FORBID] AudioSource.spatialBlend = 0 underwater.
[FORBID] Raw audio without depth-based filtering.
[FORBID] Dry/wet mixing > 0.5 on MX350.

---

## CRASH TELEMETRY & DEBUG CONSOLE

File: CrashTelemetry.cs, DebugConsole.cs

[REQ] Application.logMessageReceived -> crash_telemetry.log:
      timestamp, scene, depth, fps, vram, stacktrace.
[REQ] Auto-capture screenshot on exception -> /Saves/Debug/.
[REQ] In-game console: ~ toggle.
      Commands: fps, vram, goto <x,y,z>, spawn <prefab>, save_now.
[FORBID] Console in EA build.
[FORBID] Telemetry upload without user consent.
[FORBID] Blocking UI on crash.

---

## PERFORMANCE CI REGRESSION

File: BenchmarkRunner.cs, PerformanceThresholds.asset

[REQ] Weekly MX350 benchmark: Profiler.BeginSample("EA_Benchmark").
      Capture: FrameTime, VRAM, SetPass, Batches, GCAlloc.
[REQ] Thresholds: FrameTime <= 16.67ms, VRAM <= 1.6GB,
      SetPass <= 800, GCAlloc = 0.
[REQ] Output: performance_report.md with delta vs baseline.
[FORBID] Manual profiler checks without logging.
[FORBID] Ignore > 10% regression.
[FORBID] Skip tests on shader changes.

### Runtime Degradation Protocol
If frame time > 25ms for 3 consecutive frames, auto-degrade in order:
1. Disable vertex animation on flora
2. GPU Boids count -50%
3. Activate _QUALITY_MX350 (disable parallax/height blend)
4. Disable post-processing (Bloom, DoF, Vignette)
5. Volumetric Fog -> Half-Res

[FORBID] Static quality settings.
[FORBID] Hard crashes on performance spikes.

---

## LIGHTING PROBE GRID

File: ProbeGridGenerator.cs, BakeryConfig.asset

[REQ] Outdoor probes: 1 probe per 200m2,
      aligned to terrain height + 2m offset.
[REQ] Baking: Bakery GPU for static bases/ruins ONLY.
      Probes update via LightProbes.TetrahedralizeAsync().
[REQ] Probe density:
      High (Reefs/Bases), Low (Abyssal), None (Caves -> vertex AO).
[FORBID] Realtime GI outdoors.
[FORBID] Shadow cascades > 2 on MX350.
[FORBID] Unbaked probes in caves.

---

## ACCESSIBILITY & CONTROL REMAP

File: ControlRemapper.cs, AccessibilitySettings.cs

[REQ] Full key/gamepad rebinding via InputActionMap.
      Save to controls.json.
[REQ] Colorblind modes: PostProcessVolume profile swap
      (Protanopia / Deuteranopia / Tritanopia).
[REQ] UI scaling: CanvasScaler.scaleFactor 0.75-2.0, step 0.25.
[REQ] Difficulty toggles: O2DrainMultiplier,
      PressureDamageMultiplier, MarkerVisibility (0-3).
      Save per profile.
[FORBID] Hardcoded KeyCode in gameplay logic.
[FORBID] UI scaling > 1.5 on HUD elements.
[FORBID] Unremapped critical actions.

---

## ENDGAME EA RETENTION

File: EphemeralEventDirector.cs, DepthChallengeTracker.cs

[REQ] After 5 hours: DirectorAI spawns EphemeralEvents
      (thermal vents, drone migrations, cave collapses).
[REQ] DepthRecord tracker.
      Rewards: cosmetic HUD variants, base decor, lore fragments.
[REQ] MysteryCache: procedural loot caches,
      multi-biome coordination. Community-driven tracking.
[FORBID] Static world after story completion.
[FORBID] Dead zones without content > 500m radius.
[FORBID] Unscalable difficulty.

---

## AI CODE INTEGRATION WORKFLOW

[REQ] All AI-generated code:
1. Commit to isolated branch (feature/ai-[system])
2. Run automated profiler (GCMonitor + Frame Debugger)
   on MX350-equivalent scene
3. Verify: zero GC in hot paths, VRAM < 1.6GB, SetPass <= 600
4. Manual code review (architecture compliance,
   no hardcoded values)
5. Merge to main only after all checks pass

[FORBID] Direct push to main.
[FORBID] Unprofiled code merge.
[FORBID] Treat "works in editor" as production ready.
