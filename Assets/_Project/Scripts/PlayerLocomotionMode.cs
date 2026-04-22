namespace Hecton8.Gameplay
{
    /// <summary>
    /// Authoritative resolved locomotion mode for player movement and presentation systems.
    /// </summary>
    public enum PlayerLocomotionMode : byte
    {
        /// <summary>Grounded movement on dry exterior surfaces.</summary>
        DryGroundWalk = 0,
        /// <summary>Grounded movement inside dry pressurized interiors.</summary>
        DryInteriorWalk = 1,
        /// <summary>Grounded movement through shallow exterior water.</summary>
        ShallowWadeWalk = 2,
        /// <summary>Unsupported swim motion in the top water band near the surface.</summary>
        SurfaceSwim = 3,
        /// <summary>Unsupported full 3D underwater swim motion.</summary>
        UnderwaterSwim = 4,
        /// <summary>Heavy exosuit locomotion with seabed grounding and jump-jet support.</summary>
        ExosuitLocomotion = 5
    }
}
