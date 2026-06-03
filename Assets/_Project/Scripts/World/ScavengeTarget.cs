using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class ScavengeTarget : MonoBehaviour
    {
        [SerializeField] private int _itemId;
        [SerializeField, Min(1)] private int _harvestUnits = 1;

        public int ItemId => _itemId;
        public int HarvestUnits => _harvestUnits;

#if UNITY_EDITOR
        public void ConfigureForEditor(int itemId, int harvestUnits)
        {
            _itemId = itemId;
            _harvestUnits = Mathf.Max(1, harvestUnits);
        }
#endif
    }
}
