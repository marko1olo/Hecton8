using UnityEngine;

namespace Hecton8.World
{
    [CreateAssetMenu(fileName = "WorldPrefabFamilyProfile", menuName = "Hecton8/World/Prefab Family Profile")]
    public sealed class WorldPrefabFamilyProfile : ScriptableObject
    {
        public enum BudgetClass
        {
            Light,
            Medium,
            Heavy
        }

        [Header("Identity")]
        public string familyId = "world.family.generic";
        public string familyLabel = "Generic World Family";

        [Header("Usage")]
        public WorldSliceAnchor.SliceState defaultFidelity = WorldSliceAnchor.SliceState.Mid;
        public BudgetClass budgetClass = BudgetClass.Medium;
        public bool expectsCollision;
        public bool expectsInteraction;

        [Header("Future Integration")]
        public string futurePrefabRoot = string.Empty;
        [TextArea(2, 4)] public string gameplayRole = "Generic world family.";
    }
}
