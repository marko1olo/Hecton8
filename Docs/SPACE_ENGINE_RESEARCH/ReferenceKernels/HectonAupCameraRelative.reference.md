# Hecton AUP Camera Relative Reference

Date: 2026-05-07
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Clean-room CPU conversion for camera-relative render positions. This is not SpaceEngine source.

```csharp
using Unity.Mathematics;

public readonly struct H8AupPose
{
    public readonly long3 Sector;
    public readonly double3 LocalMeters;

    public H8AupPose(long3 sector, double3 localMeters)
    {
        Sector = sector;
        LocalMeters = localMeters;
    }
}

public static class H8AupRenderMath
{
    public static float3 ToCameraRelativeMeters(
        H8AupPose objectPose,
        H8AupPose cameraPose,
        double sectorSizeMeters)
    {
        long3 sectorDelta = objectPose.Sector - cameraPose.Sector;
        double3 meters =
            (double3)sectorDelta * sectorSizeMeters +
            (objectPose.LocalMeters - cameraPose.LocalMeters);

        return (float3)meters;
    }
}
```
