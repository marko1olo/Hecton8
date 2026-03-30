using UnityEngine;

namespace Hecton8.World
{
    [CreateAssetMenu(fileName = "WorldContentProfile", menuName = "Hecton8/World/Content Profile")]
    public sealed class WorldContentProfile : ScriptableObject
    {
        [Header("Identity")]
        public string profileId = "content.profile.generic";
        public string profileLabel = "Generic Content";

        [Header("Semantics")]
        public WorldContentSocket.ContentKind contentKind = WorldContentSocket.ContentKind.Generic;
        public WorldZoneAnchor.ZoneKind preferredZoneKind = WorldZoneAnchor.ZoneKind.Generic;
        public WorldSliceAnchor.SliceState preferredFidelity = WorldSliceAnchor.SliceState.Near;

        [Header("Future Population")]
        public string futurePrefabFamily = string.Empty;
        public string gameplayPurpose = "Generic world content.";
        public int defaultWeight = 1;
    }
}
