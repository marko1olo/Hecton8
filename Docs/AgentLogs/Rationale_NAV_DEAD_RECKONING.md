# NAV_DEAD_RECKONING Rationale

Status: `PENDING VERIFICATION`

## Pre-Code Mandate Selection

Problem: Dead reckoning touches UX, AUP precision, service registration, save persistence, crash telemetry, and zero-GC hot paths.
Solution: Constrain implementation to existing contracts, GlobalRegistry/service lookup, scalar/double math, and zero-GC UI formatting paths.
Rejected Alternatives: A MonoBehaviour singleton compass, `Camera.main`-derived heading, string-formatted HUD text, and full physical gyro simulation. These are slower, coupled, or aesthetically wrong for Deep Sea Noir.
Scalability potential: Low uses identical scalar math; Middle adds diegetic cockpit state; High and Ultra spend saved CPU on stronger UI distortion and presentation, not heavier navigation truth.
Hardware Impact: Expected low-end i3/MX350 gain is avoidance of per-frame search/string allocation and avoidance of camera transform dependency. Measured proof absent.

