# HECTON-8 Accessibility And Readability Bible

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Scope: readability, subtitles, input alternatives, color support, flashing reduction, UI scale, audio clarity, camera comfort, localization expansion, and low-tier perception.

## 0. Prime Accessibility Law

Accessibility is instrument reliability.

The player must be able to read critical state, operate critical controls, and understand critical warnings without relying on one fragile sensory channel.

Accessibility must not change gameplay truth. It may change presentation, redundancy, cadence, scale, color support, haptics, subtitles, and input method.

## 1. Critical State Redundancy

Critical states require multiple channels:

- oxygen: UI, audio, cadence, optional haptic;
- pressure/hull: UI, sound, visual crack/leak, haptic;
- route danger: light, icon/shape, audio or map cue;
- creature threat: sound, instrument disturbance, silhouette or warning;
- save/load failure: text, icon, audio, disabled reason.

Color alone is rejected.

## 2. Text And UI Scale

Required:

- UI scale control;
- no text clipping at 720p;
- long localized string test;
- fixed role hierarchy;
- high contrast mode or palette assist;
- subtitles for critical voice/audio logs;
- readable disabled reasons.

Tiny decorative labels are allowed only if noncritical. Critical labels must survive low-tier captures.

## 3. Flashing And Motion

Required options:

- reduce flashing;
- reduce camera shake;
- reduce UI degradation/glitch;
- control visor dirt intensity;
- hold-to-press alternatives for repeated strain;
- subtitle/background opacity where needed.

Reducing motion must not hide gameplay truth. It should replace the channel with clearer static/audio/UI cues.

## 4. Input Alternatives

Required:

- remapping;
- hold duration tuning;
- toggle/hold alternatives where safe;
- gamepad and keyboard navigation;
- no impossible multi-button chords for critical operations;
- clear focus state.

Input alternatives must route through the same gameplay authority. No separate easy-mode truth path.

## 5. Audio Accessibility

Required:

- subtitles for speech and mission-critical audio logs;
- captions or visual cues for critical alarms;
- dynamic range option;
- separate suit voice / warning / ambience mix controls;
- headphone mode option if implemented;
- no critical information carried by low-frequency rumble alone.

## 6. Accessibility QA Gates

Reject if:

- critical state uses color only;
- text clips at low resolution;
- camera shake cannot be reduced;
- flashing cannot be reduced;
- input cannot be remapped;
- subtitles omit critical voice/log content;
- accessibility changes gameplay truth;
- compact hardware readability fails.

## 7. Truth Boundary

Accessibility changes presentation, input method, redundancy, timing tolerance, scale, captions, haptics, and visual comfort. It does not change enemy truth, resource truth, damage truth, save identity, objective completion, or physics authority unless a separate gameplay mode explicitly owns that rule.

Every critical state must remain understandable if one sensory channel is unavailable.

## 8. Runtime Cost Boundary

Accessibility paths are production paths, not debug exceptions. They must obey the same hot-path rules as the system they modify:

- no runtime string formatting in HUD/subtitle update paths;
- no `SetActive` menu churn for repeated option changes;
- no scene search to find text, audio, camera, or input targets;
- no allocation-heavy subtitle queues;
- no per-frame font atlas rebuilds;
- no accessibility-only post effect without `rendering.md` proof;
- no remap path that changes gameplay authority, input ownership, or save identity.

Accessibility can add presentation redundancy, larger hit targets, clearer text, alternate cues, reduced motion, and input paths. It cannot become a separate truth owner for oxygen, pressure, route, mission, vehicle, construction, or AI state.

## 9. GlobalQualityWeight Scaling

Compact is the baseline for readability, not an ugly mode. Low settings must keep larger critical text, strong silhouettes, clear disabled reasons, subtitle readability, remapping, and reduced motion paths. Higher tiers may add richer degradation, screen dirt, flicker, and sensory overload only when reduction options preserve state clarity.

## 10. Proof Artifacts

Accessibility work must provide:

- 720p or low-scale screenshot;
- long localized string check;
- color-only failure check;
- subtitle/caption sample;
- remapping/input navigation proof where relevant;
- flashing/motion reduction proof;
- compact-tier readability statement.

## 11. Acceptance Sentence

Accessibility is accepted only when critical state survives multiple sensory paths, comfort options do not alter gameplay truth, and low-tier readability is proven rather than assumed.
