namespace Hecton8.Gameplay
{
    /// <summary>
    /// Presentation-only first-person swim state resolved from locomotion truth and motion cadence.
    /// </summary>
    public enum PlayerSwimPresentationMode : byte
    {
        None = 0,
        Dry = 1,
        ShallowWade = 2,
        SurfaceTread = 3,
        SurfaceStroke = 4,
        UnderwaterNeutral = 5,
        UnderwaterStroke = 6,
        UnderwaterGlide = 7,
        UnderwaterSprint = 8
    }
}
