using Hecton8.Modding;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Raised when a logistics pipe ruptures from sustained downstream blockage.
    /// </summary>
    public sealed class LogisticsPipeOverpressureLeakEvent : HectonEvent
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
