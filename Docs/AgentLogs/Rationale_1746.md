# Rationale 1746 - Camera Shake And Impact Kinetics Coordinator

Decision: Do not force Cinemachine.
Reason: Project first-party route is `CameraJuiceSystem` + DataVault + `SignalBus<CameraJuiceImpactSignal>`. No first-party Cinemachine impulse source/listener route exists; adding it would create a new dependency path instead of coordinating the actual owner.

Decision: Extend `CameraJuiceImpactSignal` reserved bytes instead of changing its size or adding a new lane.
Reason: Existing lane is fixed at 128 bytes and already consumed by the Burst camera juice path. Reusing reserved offsets preserves ABI size and producer decoupling.

Decision: Priority-band scan only for camera impact packets.
Reason: `CameraJuiceImpactSignal` now carries priority. Generic impact, high-speed impact, combat, and seismic lanes do not carry matching priority fields. The cap remains 32 records.

Decision: Consume `SettingsManager.UiMotionScale` as cached presentation motion scale.
Reason: No `Accessibility_MotionReduction` setting exists. `UiMotionScale` is the only persisted user motion comfort scalar. It is read on slow tick and scales only camera presentation shake/FOV.

Decision: Keep FOV punch from native impulse result.
Reason: This fires only for accepted impact packets in the current frame, not from decaying stored trauma. XR still suppresses FOV punch.

Scalability consequences:
- Weak devices: same 32-record cap, deterministic priority admission, existing continuous `GlobalQualityWeight` reduces radius/frequency/octave work.
- Middle tier: named profiles preserve authored impact differences without extra managed objects.
- High tier: higher quality keeps wider radius and richer octave admission.
- Ultra tier: existing continuous quality path admits extra grit taps; profile priority does not create a binary quality switch.

Rejected:
- New Cinemachine dependency.
- New `CameraShakeSignal` lane.
- New persisted settings field without UI/settings ownership.
- Raising signal cap above 32.
