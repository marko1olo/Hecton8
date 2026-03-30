using UnityEngine;

namespace Hecton8.Environment
{
    [CreateAssetMenu(fileName = "FaunaFamilyProfile", menuName = "Hecton/Environment/Fauna Family Profile", order = 105)]
    public sealed class HectonFaunaFamilyProfile : ScriptableObject
    {
        [Header("Identity")]
        public string familyId = "fauna.family.generic";
        public string familyLabel = "Generic Fauna Family";

        [Header("Behavior")]
        public string ambientLife = "mixed";
        public string threatStyle = "mixed";
        public string signaturePredator = "none";
        public string encounterRhythm = "balanced";

        [Header("Direction")]
        [TextArea(2, 4)] public string gameplaySummary = "Generic fauna role.";
        [TextArea(2, 4)] public string ambienceSummary = "Generic ambient life.";
    }
}
