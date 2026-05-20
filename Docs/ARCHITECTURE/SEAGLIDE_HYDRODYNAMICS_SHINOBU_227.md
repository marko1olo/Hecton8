# Seaglide Hydrodynamics SHINOBU_227

Authority:
- `MantaScooter` is a request producer only. It no longer stores or reads `Rigidbody`.
- `SeaglideHydrodynamicsRuntime` owns Vault buffers, Burst scheduling, telemetry, and 1000-record mock generation.
- `PhysicsApplySystem.SeaglideQueue` is the only bridge that resolves the player body and queues force application.

Hot-path data:
- `SeaglideStateDTO`: 64 bytes, explicit layout, AUP at 0, velocity at 24, battery at 36, flags at 40.
- `SeaglidePropulsionRequestDTO`: 128 bytes, explicit layout, current/previous `double3` AUP for Doppler and origin-shift safety.
- Visual/audio/cavitation DTOs are separate from physical state and marked rollback-excluded.

Scalability:
- `HomeostasisBrain.GlobalQualityWeight` continuously blends cheap dominant-axis drag/current fakes toward full quadratic drag and trilinear current sampling.
- Battery metabolism cadence interpolates between slow and fixed cadence without binary tier switches.

