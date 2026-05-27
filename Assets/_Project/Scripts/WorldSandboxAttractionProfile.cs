using UnityEngine;

namespace Hecton8.World
{
    [CreateAssetMenu(fileName = "WorldSandboxAttractionProfile", menuName = "Hecton8/World/Sandbox Attraction Profile")]
    public sealed class WorldSandboxAttractionProfile : ScriptableObject
    {
        [Header("Identity")]
        public string profileId = "world.sandbox.generic";
        public string profileLabel = "Generic Sandbox Attraction";

        [Header("World Pull")]
        [TextArea(2, 4)] public string entryRead = "A strong readable landmark hints that this water is worth checking.";
        [TextArea(2, 4)] public string ambientValue = "Routine value sits in the area and rewards natural wandering.";
        [TextArea(2, 4)] public string detourValue = "Side value rewards curiosity without becoming mandatory.";
        [TextArea(2, 4)] public string shelterRead = "A small shelter or reorientation pocket lets the player breathe and re-read the space.";
        [TextArea(2, 4)] public string pressureRead = "Pressure rises in a readable way as the player pushes farther from route control.";
        [TextArea(2, 4)] public string deepLure = "A strong deeper attraction promises rarer value without forcing commitment.";
        [TextArea(2, 4)] public string storyLure = "The place hints at narrative importance, history, or unresolved mystery.";
        [TextArea(2, 4)] public string returnValue = "Returning later should feel worthwhile because the zone still has meaning.";

        [Header("Sandbox Rules")]
        [TextArea(2, 4)] public string freedomRule = "The player may enter from any side, leave early, and build a personal route.";
        [TextArea(2, 4)] public string curiosityRule = "Interesting side reads should exist without becoming chores.";
        [TextArea(2, 4)] public string crosslinkRule = "Nearby pockets and landmarks should connect into a discoverable mesh, not a pipe.";
        [TextArea(2, 4)] public string reentryRule = "A repeat visit should feel smarter and faster after learning the water.";
        [TextArea(2, 4)] public string masteryRule = "Mastery means owning the space and choosing bold shortcuts by memory.";

        [Header("Reading")]
        [TextArea(2, 4)] public string playerPromise = "This area promises readable, memorable sandbox exploration.";
        [TextArea(2, 4)] public string memoryRule = "Shape language and landmarks should do more work than UI directions.";
        [TextArea(2, 4)] public string dangerRule = "Danger should be signaled early and intensify by readable gradients, not arbitrary spikes.";
    }
}
