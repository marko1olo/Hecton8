# HECTON-8 Settings, Options, And User Configuration Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: graphics/audio/input/accessibility settings, saveable options, quality profiles, device detection, menu settings UI, config persistence, and settings proof gates.

## Prime Law

Settings are control surfaces for survival readability, comfort, and hardware safety. They are not a dumping ground for arbitrary toggles.

Every setting must have owner, range, persistence key, default, validation rule, apply timing, rollback behavior, and player-facing reason. HECTON-8 rejects binary quality switches, hidden settings that alter gameplay truth, unsafe defaults, and options that create unsupported platform promises.

## Truth Ownership

Settings owns user preference state, validation, persistence, apply staging, and UI option metadata. It does not own rendering truth, audio mix truth, input semantics, accessibility features, platform support, or gameplay authority.

Feature owners declare allowed ranges. Settings stores and applies validated values at approved phases. Runtime systems consume immutable settings snapshots or typed change events.

## Setting Contract

Each setting must define:

- stable key;
- owning domain;
- type and range;
- default by platform/tier if needed;
- apply phase: immediate, menu-only, restart, scene reload, or build-time;
- validation/clamp rule;
- persistence route;
- localization label/description id;
- accessibility impact;
- `GlobalQualityWeight` relationship if visual/performance.

## Quality And Hardware

No binary low/ultra split. Quality must use continuous `GlobalQualityWeight` and domain-specific scalars.

Graphics options may expose presets, but internal systems still consume continuous weights, budgets, and feature caps. A setting may reduce presentation density, cadence, resolution, or optional diagnostics. It must not change gameplay truth, save identity, network authority, DTO layout, or item rules.

## Runtime Law

Required:

- no per-frame PlayerPrefs reads;
- no string-key lookup in hot paths;
- no setting application by scene search;
- no runtime material/shader asset mutation for settings changes;
- bounded apply events;
- rollback if a display/resolution setting fails;
- save settings atomically.

## GlobalQualityWeight Scaling

`GlobalQualityWeight` is the primary visual/performance scalar. Settings may set target weight or domain budgets. It may not be replaced by independent binary toggles that desync systems.

Compact defaults preserve readability, input comfort, core audio warnings, and route cues. High/Ultra presets add visual density only after proof.

## Production Packet

Any settings, options, quality profile, control option, or user configuration change must declare:

- setting id, owner, valid range, default, and persistence key;
- apply timing: boot, menu, scene transition, or runtime event;
- rollback/fallback for display, input, audio, and accessibility settings;
- relationship to `GlobalQualityWeight` or domain-specific budget;
- save/load and invalid-value clamp behavior;
- Compact menu proof and localization expansion proof;
- profiler/GC proof when runtime setting apply code changes.

Settings that are binary quality switches, unsafe user-state mutations, or unreadable menu rows are rejected.

## Proof Artifacts

Settings work must provide:

- setting schema table;
- persistence proof;
- invalid value clamp test;
- UI screenshot with long localized labels;
- apply timing list;
- rollback/fallback behavior for display-sensitive options;
- compact/high scaling note;
- no hot-path settings read scan if implemented.

## Rejection Gates

Reject:

- setting with no owner;
- setting that silently changes gameplay truth;
- binary quality switch replacing continuous scaling;
- PlayerPrefs or string lookup in hot paths;
- setting that cannot be localized;
- graphics claims without platform/profiler proof;
- options UI that is generic SaaS-like instead of an instrument panel.

## Acceptance Sentence

Settings are accepted only when every option has owner, range, persistence, safe apply timing, localization, proof, and continuous scaling behavior that preserves gameplay truth.
