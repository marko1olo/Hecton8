# Hecton AUP Camera Relative Reference

Date: 2026-05-07
Status: PENDING VERIFICATION

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
