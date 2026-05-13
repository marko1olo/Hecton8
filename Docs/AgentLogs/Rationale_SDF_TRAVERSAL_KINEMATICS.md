# Rationale_SDF_TRAVERSAL_KINEMATICS

Status: PENDING VERIFICATION

## Decision 0 - Batch Bootstrap
Problem: Player tight-gap swimming requires SDF-aware movement without coupling to unfinished voxel, camera, haptic, stress, or audio agents.
Solution: Start from existing locomotion code and prefer cached interfaces or existing NativeQueue/EventBus signal contracts. Use SDF math only where gameplay collision correctness needs it; camera roll, haptic scrape, audio scrape, and stress are presentation/feedback signals.
Rejected Alternatives: Direct concrete references to camera, audio, haptic, stress, or voxel runtime classes. Standard Unity `ComputePenetration` as player movement authority because prompt explicitly requires purge.
Scalability potential: Low uses 4-tap tetrahedral gradient and conservative correction. Middle uses 6-tap central gradient. High/Ultra can raise visual feedback density through camera roll/audio/haptic cadence without increasing collision truth.
Hardware Impact: Target is i3/MX350. Expected low-tier gain comes from avoiding main-thread penetration recovery loops and keeping squeeze math bounded. Measured proof absent.

