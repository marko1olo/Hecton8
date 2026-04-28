using UnityEngine;

namespace Hecton8.World
{
    internal readonly struct SamplingSnapshot
    {
        public SamplingSnapshot(Vector3 runtimeCenter, Vector3 absoluteCenter, int centerCellX, int centerCellZ, float captureTime)
        {
            RuntimeCenter = runtimeCenter;
            AbsoluteCenter = absoluteCenter;
            CenterCellX = centerCellX;
            CenterCellZ = centerCellZ;
            CaptureTime = captureTime;
        }

        public Vector3 RuntimeCenter { get; }

        public Vector3 AbsoluteCenter { get; }

        public int CenterCellX { get; }

        public int CenterCellZ { get; }

        public float CaptureTime { get; }

        public Vector3 Center => AbsoluteCenter;
    }
}
