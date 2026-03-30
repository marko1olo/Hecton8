using UnityEngine;

namespace Hecton8.World
{
    [CreateAssetMenu(fileName = "WorldExpeditionLoopProfile", menuName = "Hecton8/World/Expedition Read Profile")]
    public sealed class WorldExpeditionLoopProfile : ScriptableObject
    {
        [Header("Identity")]
        public string profileId = "zone.loop.generic";
        public string profileLabel = "Generic Expedition Read";

        [Header("Exploration Reads")]
        [TextArea(1, 3)] public string entryBeat = "A readable landmark or silhouette invites first contact with the area.";
        [TextArea(1, 3)] public string routineBeat = "Common value is easy to spot without forcing one exact sweep.";
        [TextArea(1, 3)] public string reliefBeat = "A short shelter or reset pocket can be found by reading the space.";
        [TextArea(1, 3)] public string pressureBeat = "Risk rises naturally where depth, threat, or visibility begin to turn against the player.";
        [TextArea(1, 3)] public string payoffBeat = "A stronger lure exists deeper in, but the player chooses if and when to commit.";
        [TextArea(1, 3)] public string exitBeat = "Return stays readable through remembered landmarks, not through a forced track.";

        [Header("Sandbox Reading")]
        [TextArea(2, 4)] public string playerFreedomRule = "This is not a forced path. The loop is a natural pull, not a script.";
        [TextArea(2, 4)] public string softProgressionPull = "The zone should tempt the player toward one stronger route without blocking free roaming.";
        [TextArea(2, 4)] public string optionalDetourRule = "Detours should still pay out something readable, even if the player ignores the strongest lure in the area.";
        [TextArea(2, 4)] public string returnLogic = "A player who leaves early should still feel they learned something useful about the place.";
        [TextArea(2, 4)] public string masteryLogic = "A player who knows the zone well should be able to cut their own faster, riskier, smarter line.";

        [Header("Reading")]
        [TextArea(2, 4)] public string playerPromise = "The area promises readable value, readable danger, and room for player choice.";
        [TextArea(2, 4)] public string routeMemoryRule = "The player should remember the place through shapes, safety pockets, and pressure shifts.";
        [TextArea(2, 4)] public string failureMode = "If the space loses readability, it stops feeling like a sandbox and turns into noise.";
    }
}
