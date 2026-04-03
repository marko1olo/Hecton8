using UnityEngine;
using System.Collections.Generic;

namespace Hecton8.Gameplay
{
    public enum FieldTargetRole
    {
        Generic = 0,
        CargoLight = 1,
        CargoWork = 2,
        CargoHeavy = 3,
        CargoOverweight = 4,
        RouteAnchor = 5,
        RouteRelay = 6,
        RouteFrontier = 7,
        SalvagePickup = 8,
        ResourceNodeActive = 9,
        ResourceNodeDepleted = 10,
        ServiceDamaged = 11,
        ServiceFlooded = 12,
        ServiceControl = 13,
        HazardProbe = 14,
        ResourceCache = 15,
        StructureRelay = 16,
        ExpeditionCheckpoint = 17,
        BioformDormant = 18,
        BioformAggressive = 19,
        BioformFractured = 20,
        BioformDown = 21,
        ConstructionSocket = 22,
        ConstructionBlocked = 23,
        ConstructionClear = 24,
        PowerGeneration = 25,
        PowerRelay = 26,
        PowerLoad = 27
    }

    [DisallowMultipleComponent]
    public sealed class FieldTargetDescriptor : MonoBehaviour
    {
        private static readonly List<FieldTargetDescriptor> _ActiveDescriptors = new List<FieldTargetDescriptor>(64);

        [SerializeField] private FieldTargetRole role = FieldTargetRole.Generic;
        [SerializeField] [TextArea(2, 4)] private string operatorNote = string.Empty;

        public FieldTargetRole Role => role;
        public string OperatorNote => operatorNote;
        public static int ActiveCount => _ActiveDescriptors.Count;

        private void OnEnable()
        {
            if (!_ActiveDescriptors.Contains(this))
                _ActiveDescriptors.Add(this);
        }

        private void OnDisable()
        {
            _ActiveDescriptors.Remove(this);
        }

        private void OnDestroy()
        {
            _ActiveDescriptors.Remove(this);
        }

        public void Configure(FieldTargetRole targetRole, string note)
        {
            role = targetRole;
            operatorNote = note ?? string.Empty;
        }

        public static FieldTargetDescriptor GetActiveDescriptorAt(int index)
        {
            return index >= 0 && index < _ActiveDescriptors.Count
                ? _ActiveDescriptors[index]
                : null;
        }

        public static bool TryResolve(Component source, out FieldTargetDescriptor descriptor)
        {
            descriptor = null;
            if (source == null)
                return false;

            descriptor = source.GetComponent<FieldTargetDescriptor>() ?? source.GetComponentInParent<FieldTargetDescriptor>();
            return descriptor != null;
        }
    }
}
