using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Authoring template for autonomous drone tiers. Assets can be created later without changing fleet runtime code.
    /// </summary>
    [CreateAssetMenu(fileName = "AutomataTemplate", menuName = "Hecton8/Construction/Automata Template")]
    public sealed class AutomataTemplate : ScriptableObject
    {
        public enum AutomataTier : byte
        {
            MkIScrubber = 0,
            MkIIWelder = 1,
            GuardianUnit = 2
        }

        [Header("Identity")]
        [SerializeField] private AutomataTier tier = AutomataTier.MkIScrubber;
        [SerializeField] private bool autoPopulatePreset = true;

        [Header("Mobility")]
        [SerializeField, Min(0.1f)] private float cruiseSpeed = 5f;
        [SerializeField, Min(0.1f)] private float acceleration = 12f;
        [SerializeField, Min(0.1f)] private float batteryCapacityNormalized = 1f;
        [SerializeField, Min(0f)] private float batteryDrainPerSecond = 0.03f;

        [Header("Service Payload")]
        [SerializeField, Min(0)] private int solderCapacityUnits = 12;
        [SerializeField, Min(0f)] private float repairThroughputPerSecond = 12f;

        [Header("Defense Payload")]
        [SerializeField, Min(0f)] private float guardianPushForce = 0f;
        [SerializeField, Min(0f)] private float guardianThreatRadius = 0f;

        public AutomataTier Tier => tier;
        public float CruiseSpeed => cruiseSpeed;
        public float Acceleration => acceleration;
        public float BatteryCapacityNormalized => batteryCapacityNormalized;
        public float BatteryDrainPerSecond => batteryDrainPerSecond;
        public int SolderCapacityUnits => solderCapacityUnits;
        public float RepairThroughputPerSecond => repairThroughputPerSecond;
        public float GuardianPushForce => guardianPushForce;
        public float GuardianThreatRadius => guardianThreatRadius;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!autoPopulatePreset)
                return;

            switch (tier)
            {
                case AutomataTier.MkIScrubber:
                    cruiseSpeed = 4.25f;
                    acceleration = 10f;
                    batteryCapacityNormalized = 1.35f;
                    batteryDrainPerSecond = 0.022f;
                    solderCapacityUnits = 18;
                    repairThroughputPerSecond = 11f;
                    guardianPushForce = 0f;
                    guardianThreatRadius = 0f;
                    break;

                case AutomataTier.MkIIWelder:
                    cruiseSpeed = 7.5f;
                    acceleration = 16f;
                    batteryCapacityNormalized = 0.85f;
                    batteryDrainPerSecond = 0.041f;
                    solderCapacityUnits = 8;
                    repairThroughputPerSecond = 20f;
                    guardianPushForce = 0f;
                    guardianThreatRadius = 0f;
                    break;

                case AutomataTier.GuardianUnit:
                    cruiseSpeed = 6.2f;
                    acceleration = 15f;
                    batteryCapacityNormalized = 1.05f;
                    batteryDrainPerSecond = 0.05f;
                    solderCapacityUnits = 2;
                    repairThroughputPerSecond = 4f;
                    guardianPushForce = 18f;
                    guardianThreatRadius = 18f;
                    break;
            }
        }
#endif
    }
}
