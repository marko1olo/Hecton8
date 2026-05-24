using UnityEngine;

namespace Hecton8.World
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal readonly struct SamplingSnapshot
    {
        public readonly Vector3 RuntimeCenter;
        public readonly Vector3 AbsoluteCenter;
        public readonly int CenterCellX;
        public readonly int CenterCellZ;
        public readonly float CaptureTime;

        public SamplingSnapshot(Vector3 runtimeCenter, Vector3 absoluteCenter, int centerCellX, int centerCellZ, float captureTime)
        {
            RuntimeCenter = runtimeCenter;
            AbsoluteCenter = absoluteCenter;
            CenterCellX = centerCellX;
            CenterCellZ = centerCellZ;
            CaptureTime = captureTime;
        }

        public static Vector3 GetCenter(in SamplingSnapshot snapshot)
        {
            return snapshot.AbsoluteCenter;
        }
    }
}
