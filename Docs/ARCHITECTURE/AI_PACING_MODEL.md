# AI Pacing Model

Date: 2026-05-19

Status: STATIC CONTRACT / RUNTIME PENDING

## Purpose

This file closes the active architecture reference expected by `HEADLESS_ECOSYSTEM_SIMULATION.md`.

It defines pacing ownership for Echelon 3 fauna/ecosystem and Echelon 9 quality-control systems without turning static source presence into runtime readiness.

## Source Anchors

- `Assets/_Project/Scripts/HectonDirectorAI.cs`

- `Assets/_Project/Scripts/FaunaDirector.cs`

- `Assets/_Project/Scripts/World/EcosystemDirector.cs`

- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`

- `Assets/_Project/Scripts/Core/SystemDispatcher.cs`

- `Assets/_Project/Scripts/Core/HomeostasisBrain.cs`

## Contract

AI pacing is a deterministic pressure budget, not a cinematic script rail.

- `HectonDirectorAI` owns encounter pressure and player-facing escalation.

- `EcosystemDirector` owns cold population pressure, migration, predator/prey balance, and headless biomass drift.

- `FaunaDirector` owns visible fauna activation and handoff between data-only and presented actors.

- `PersistentWorldRegistry` owns hibernated entity records and sector reconciliation.

- `SystemDispatcher` owns cadence; pacing systems must register into dispatcher lanes rather than adding private Unity loops.

- `HomeostasisBrain.GlobalQualityWeight` is the continuous quality scalar. Do not use binary low/ultra switches as pacing truth.

## Quality-Curve Rules

- Weak devices: reduce active cognition, presentation radius, simultaneous threats, and update cadence first. Keep deterministic sector state.
- Intermediate weights: preserve one clear threat line and background ecosystem drift.
- High-quality weights: add secondary tells, richer flocking, wider audio foreshadowing, and more concurrent non-critical fauna.
- Maximum-quality weights: spend saved budget on perception richness and density, not on unbounded physical truth.

## Proof Gates

Before calling this model ready in runtime, provide fresh artifacts for:

- Unity import and Console state.

- Play Mode route through bootstrap, world load, and one encounter.

- Profiler and GCMonitor capture with pacing systems active.

- Save/load of hibernated fauna records.

- Player-build or platform run for target hardware.

Until those artifacts exist, this file is contract orientation only.
