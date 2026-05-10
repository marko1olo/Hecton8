// ══════════════════════════════════════════════════════════════════
// EnvironmentState.cs
// Perechislenie sostoyaniy okruzhayuschey sredy ekzoluny Gekton
// ══════════════════════════════════════════════════════════════════

/// <summary>
/// Sostoyaniya atmosfery mira.
/// Ispolzuetsya HectonAtmosphereManager i vsemi podpischikami OnStateChanged.
/// </summary>
public enum EnvironmentState
{
    /// <summary>Dnevnaya poverhnost — standartnoe osveschenie.</summary>
    SURFACE_DAY,

    /// <summary>Nochnaya poverhnost — solntse nizhe gorizonta.</summary>
    SURFACE_NIGHT,

    /// <summary>Pod vodoy — kamera igroka nizhe urovnya vody.</summary>
    UNDERWATER,

    /// <summary>Velikoe Zatmenie — redkoe kosmicheskoe sobytie.</summary>
    ECLIPSE
}