# Status_MANDATE_AUDIT

Date: 2026-05-17
Domain: Docs/Mandates
Status: PENDING VERIFICATION

- [x] Read authority spine and relevant mandates | DOD: static source/doc read via CLI; rejected relying on chat prompt as authority; runtime microseconds saved: 0.
- [x] Compare new polish prompt clauses against registry | DOD: targeted `rg` scans for ARM64 layout, editor bridges, I/O, Blackbox, GPU group sizing; rejected bulk-loading all 78 old mandates as context noise; runtime microseconds saved: 0.
- [x] Patch missing mandate coverage | DOD: added central ARM64 runtime struct law and designer CSV/binary bridge law; rejected changing runtime code or project settings; runtime microseconds saved: 0.
- [x] Resolve direct mandate conflict | DOD: corrected `ItemData` example from Pack=4/ambiguous 20B to 24B explicit ARM64-safe layout; rejected packed runtime DTOs; runtime microseconds saved: 0.
- [x] Clean damaged prompt file | DOD: replaced mojibake prompt dump with UTF-8 prompt template subordinate to authority spine; rejected treating prompt dump as project authority; runtime microseconds saved: 0.
- [x] Verify text-level consistency | DOD: registry count and keyword scans must pass; no Unity compile required for docs-only changes; runtime microseconds saved: 0.

No C# code, asmdef, Unity scene, prefab, asset, shader, or project setting was modified by MANDATE_AUDIT.

## 2026-05-18 Re-Audit

- [x] Re-read status/rationale before response | DOD: anti-amnesia file read via CLI; rejected relying on 2026-05-17 chat memory; runtime microseconds saved: 0.
- [x] Re-read current prompt template and registry | DOD: CLI read of `Docs/takoi prompt dlya gemini.txt`, `.agents-skills/README.md`, ARM64/layout, designer bridge, GPU warp sizing, and async/MMF mandates; rejected raw chat prompt as authority; runtime microseconds saved: 0.
- [x] Re-scan conflict surfaces | DOD: `rg` scans for mandate count, runtime `Pack=1/Pack=4`, GPU group sizing, designer bridge, and MicroSD/file I/O terms; rejected changing mandates without drift evidence; runtime microseconds saved: 0.
- [x] Preserve mandates unchanged | DOD: no new contradiction found; current registry-compatible prompt is stricter than the raw chat prompt on GPU group sizing; runtime microseconds saved: 0.
