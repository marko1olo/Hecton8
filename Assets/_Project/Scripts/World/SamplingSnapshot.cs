using UnityEngine;

namespace Hecton8.World
{
    internal readonly struct SamplingSnapshot
    {
        public SamplingSnapshot(Vector3 playerPosition, int centerCellX, int centerCellZ, float captureTime)
        {
            PlayerPosition = playerPosition;
            CenterCellX = centerCellX;
            CenterCellZ = centerCellZ;
            CaptureTime = captureTime;
        }

        public Vector3 PlayerPosition { get; }

        public int CenterCellX { get; }

        public int CenterCellZ { get; }

        public float CaptureTime { get; }

        public Vector3 Center => PlayerPosition;
    }
}
