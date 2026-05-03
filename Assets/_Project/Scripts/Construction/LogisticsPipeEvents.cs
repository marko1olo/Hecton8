using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Raised when a logistics pipe ruptures from sustained downstream blockage.
    /// </summary>
    internal readonly struct LogisticsPipeOverpressureLeakEvent
    {
        public LogisticsPipeOverpressureLeakEvent(int pipeInstanceId, Vector3 worldPosition, float overpressureStress, int itemHashId)
        {
            PipeInstanceId = pipeInstanceId;
            WorldPosition = worldPosition;
            OverpressureStress = overpressureStress;
            ItemHashId = itemHashId;
        }

        public int PipeInstanceId { get; }
        public Vector3 WorldPosition { get; }
        public float OverpressureStress { get; }
        public int ItemHashId { get; }
    }
}
