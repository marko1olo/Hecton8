// ============================================================================
// HECTON-8 — HazardType.cs
// Tipy lokalnyh ugroz okruzhayuschey sredy.
// ============================================================================

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Perechislenie tipov opasnyh zon.
    /// Ispolzuetsya HectonHazardManager dlya filtratsii i rascheta vozdeystviya.
    /// </summary>
    public enum HazardType
    {
        /// <summary>Radiatsionnoe izluchenie (vliyaet na tselostnost i vyzyvaet pomehi HUD).</summary>
        Radiation,

        /// <summary>Ekstremalno vysokaya temperatura (termalnye istochniki, lava).</summary>
        Heat,

        /// <summary>Toksichnye gazy ili himicheskoe zagryaznenie.</summary>
        Toxicity,

        /// <summary>Biologicheskaya ugroza (spory, patogeny).</summary>
        Biohazard
    }
}
