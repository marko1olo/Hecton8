# STEER_2501_2505_LEAK_AND_PROOF_GATE

Target: `Продолжить работу по логам` Unity owner.
Date: 2026-06-04.
Sources:
- `Docs/Reports/Batch25/2501_WEATHEREVENTS_PERSISTENT_LEAK_AUDIT.md`
- `Docs/Reports/Batch25/2505_VISUAL_PROOF_WATCHDOG_GATE.md`

Use after current compile/import/ILPP quiets down.

2501 leak finding:
- `WeatherEvents` Persistent leak is real in the inspected log.
- Stack: `WeatherEvents.EnsureInitialized()` -> `WeatherEvents.Register()` -> `HectonCelestialEngine.OnEnable()`.
- Owner is `Assets/_Project/Scripts/Environment/WeatherEvents.cs`, not `HectonCelestialEngine`.
- Leak source: two static `NativeQueue<WeatherEventPayload>` lanes, `_pendingEvents` and `_nextFrameEvents`.
- Disk already contains another agent's uncommitted editor lifecycle cleanup patch in `WeatherEvents.cs`; do not overwrite it blindly.
- Current patch is plausible but unverified. Need fresh Unity reload/play-exit proof after compile.

Required clean-leak proof:
1. Let current compile/import finish.
2. Clear baseline / identify fresh log window.
3. Enter Play through normal route.
4. Exit Play.
5. Trigger or observe domain reload / script reload if applicable.
6. Confirm no `Leak Detected : Persistent allocates` stack for `WeatherEvents`.

2505 visual proof gate:
- 1474 is reject-only evidence.
- No 1475 packet/manifest exists yet.
- Any new visual claim requires six views plus manifest and clean log tail:
  - surface/coast/Aegir,
  - shoreline close foam/wet contact,
  - underwater 0-5 m,
  - underwater 20-50 m route,
  - Aegir/celestial long,
  - low-oblique regression,
  - manifest with checksums/timestamps/camera/depth/quality/toggles/log path,
  - log tail newer than final screenshot and stable.

Do not accept or claim progress from diagnostics alone. Do not write packet screenshots under `Assets/Screenshots`.
