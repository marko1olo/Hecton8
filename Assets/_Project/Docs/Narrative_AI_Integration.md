# Hecton8 — Narrative & AI Director Integration

## Overview
This document outlines the technical integration between the **Narrative Discovery** system and the **AI Director (Atlas-6)**. The goal is to create a feedback loop where narrative progression influences game tension, and the AI Director triggers narrative moments.

## Systems

### 1. HectonDirectorAI (Atlas-6)
The AI Director calculates a `TensionScore` (0-100) and manages game phases.
- **New Tension Factors**:
  - **Depth**: Tension increases as the player dives deeper.
  - **Panic (Speed)**: Sudden high-velocity movement increases tension.
  - **Loot Drought**: Tension increases if the player hasn't collected resources recently.
- **Narrative Feedback**:
  - Finding a `NarrativeDiscovery` object reduces `TensionScore` by a significant amount (e.g., -20).
  - This incentivizes exploration and provides "pacing relief" during high-tension phases.
- **Triggers**:
  - In the `Relax` phase, the Director may trigger a `RareDiscovery` event to guide the player toward lore or valuable wreckage.

### 2. HectonNarrativeDirector
Tracks global narrative state and depth progression.
- **Discovery Tracking**: Persistently stores IDs of discovered objects.
- **Depth Tiers**: Fires events when the player reaches specific depth milestones (Tier 1-4).
- **AI Sync**: Listens for `RareDiscovery` requests from the AI Director to signal readiness for specific narrative beats.

## Event Communication
Communication is decoupled via static event buses:
- `NarrativeEvents.OnDiscoveryMade`: Fired by interactables, consumed by AI Director and Narrative Director.
- `NarrativeEvents.OnDepthTierReached`: Fired by Narrative Director based on player position.
- `InteractionEvents.OnItemCollected`: Fired by survival/inventory systems, consumed by AI Director to track loot frequency.
- `HectonDirectorAI.OnRequestRareDiscovery`: Fired by AI Director, consumed by Narrative Director or spawning systems.

## Optimization Notes
- All systems utilize the `GameTickManager` (`ISlowTickable`) to avoid `Update()` overhead.
- Zero-GC implementation for all tension calculations and event handling.
- Registration-based predator tracking for O(1) lookup during tension scans.
