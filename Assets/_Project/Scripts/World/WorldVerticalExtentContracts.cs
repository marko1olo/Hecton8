namespace Hecton8.World
{
    /// <summary>
    /// SINGLE AUTHORED SOURCE for the world's vertical extent window.
    /// <para>
    /// Follows the <see cref="WorldWaterLevelCalibrationMath"/> idiom
    /// (World/Contracts/WorldWaterLevelCalibrationContracts.cs:163-170): a contracts-style static class of
    /// consts that every consumer reads, so the owner's vertical decision is a one-line change here
    /// instead of five edits scattered across a MapMagic node, two harnesses and a scene builder.
    /// </para>
    /// <para>
    /// PLACEMENT NOTE - this file is deliberately NOT under World/Contracts/. That folder carries
    /// <c>Hecton8.World.Contracts.asmdef</c> with <c>"autoReferenced": false</c>, and neither
    /// <c>Hecton8.Plugins</c> (owns the MapMagic node) nor <c>Hecton8.Editor</c> (owns ErosionTestHarness and
    /// CreateSandboxV2) lists that assembly in its references - Unity does not resolve asmdef references
    /// transitively, so a const placed there would be CS0012-invisible to three of the four consumers.
    /// Scripts/World/ belongs to <c>Hecton8.Core</c>, which all three of those assemblies already reference,
    /// and this file therefore compiles for every consumer with zero asmdef edits. Existing in-tree
    /// precedent for a contracts file at this exact path: <c>Scripts/World/WorldTerrainDetailContracts.cs</c>.
    /// </para>
    /// <para>
    /// NOT A DESIGN DECISION. Every value below is transcribed from what the tree already does today, so
    /// consolidating changed no generated geometry. The final vertical extent of HECTON-8 is the owner's
    /// call and is still open - see the envelope arithmetic on <see cref="DefaultVerticalSpanMeters"/>.
    /// </para>
    /// </summary>
    public static class WorldVerticalExtentMath
    {
        /// <summary>
        /// Bottom of the normalisation window, in world metres.
        /// <para>
        /// Transcribed from <c>HectonSandboxAbyssalShelfMapMagicNode.lowWorldY</c> field initialiser
        /// (Scripts/Plugins/MapMagic/HectonSandboxAbyssalShelfMapMagicNode.cs, "Low Y m").
        /// </para>
        /// <para>
        /// This is NOT a depth budget and NOT a seafloor floor. It is the lower end of the linear window
        /// that maps absolute geology metres onto the 0..1 MapMagic heightmap: it is the low clamp in
        /// <c>HectonSandboxAbyssalShelfMath.EvaluateFullHeightMeters</c>
        /// (Scripts/World/HectonSandboxAbyssalShelfJobs.cs:215) and the <c>lowWorldY</c> argument of
        /// <c>NormalizeHeight01</c> (same file, :295-297, called from the differential job at :889-892).
        /// Changing it rescales every slope, cliff, shelf break and trench in the world.
        /// </para>
        /// </summary>
        public const float DefaultLowWorldY = -5000f;

        /// <summary>
        /// Top of the normalisation window, in world metres. Transcribed from
        /// <c>HectonSandboxAbyssalShelfMapMagicNode.highWorldY</c> field initialiser ("High Y m").
        /// Consumed through <c>ResolveSlopeLockedHighWorldY</c>
        /// (Scripts/World/HectonSandboxAbyssalShelfJobs.cs:219-222), which enforces
        /// <c>max(HighWorldY, LowWorldY + 1)</c>.
        /// </summary>
        public const float DefaultHighWorldY = 2000f;

        /// <summary>
        /// Height of the normalisation window in metres: 7000 m. Derived, never typed by hand.
        /// <para>
        /// This is the value <c>TerrainData.size.y</c> (MapMagic <c>MapMagicObject.globals.height</c>) is
        /// required to equal. The runtime guard that polices it lives in
        /// <c>HectonSandboxAbyssalShelfMapMagicNode.Generate</c> under
        /// <c>UNITY_EDITOR || DEVELOPMENT_BUILD</c>, and warns when
        /// <c>|globals.height - (HighWorldY - LowWorldY)| &gt; 1</c>. A mismatch does not fail generation -
        /// it silently collapses every vertical feature by the ratio of the two numbers.
        /// </para>
        /// </summary>
        /// <remarks>
        /// WHAT THE GENERATOR CAN ACTUALLY PRODUCE - documentation only, deliberately not consts, because
        /// every input below is owned by another file and copying it here would create a fifth drifting
        /// duplicate of a number this file exists to de-duplicate.
        /// <para>
        /// Chain: <c>WorldMacroGeologyParams.CreateDefault</c> sets <c>HadalDepthMeters = 4600f</c>
        /// (Scripts/World/WorldMacroGeologyFields.cs:51); <c>TrySanitizeParams</c> (:259) leaves it at 4600
        /// because <c>AbyssDepthMeters + 1000 = 3950</c> is lower; the evaluator's final clamp is
        /// <c>depth = clamp(depth, -620f, HadalDepthMeters)</c> (:1342) and it returns
        /// <c>WaterSurfaceY - depth</c> (:1381). The sandbox path sets
        /// <c>WaterSurfaceY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY</c> = 14.02
        /// (Scripts/World/HectonSandboxAbyssalShelfJobs.cs:189), then adds a meso detail delta bounded by
        /// <c>MaxMesoDeltaMeters = 70f</c> (same file, :208; hard-clamped to +/-maxDelta at
        /// Scripts/World/WorldTerrainDetailContracts.cs:563-566).
        /// </para>
        /// <para>
        /// Containment interval: <c>Y in [14.02 - 4600 - 70, 14.02 + 620 + 70] = [-4655.98, +704.02]</c>,
        /// a real relief span of 5360.0 m. The upper bound is loose: the soft ceiling at
        /// WorldMacroGeologyFields.cs:1336-1341 compresses toward an asymptote of <c>depth = -600</c>, so
        /// the -620 clamp never binds and the true maximum is about +684 m.
        /// </para>
        /// <para>
        /// Consequence: this window is 1640 m WIDER than the relief the generator can emit, so roughly 23%
        /// of the 0..1 heightmap range is dead headroom that no terrain ever occupies. That is a calibration
        /// question for the owner, not a bug to silently close.
        /// </para>
        /// </remarks>
        public const float DefaultVerticalSpanMeters = DefaultHighWorldY - DefaultLowWorldY;

        /// <summary>
        /// DIVERGENT VALUE, RECORDED NOT BLESSED: 4000 m.
        /// <para>
        /// This is the <c>MapMagicObject.globals.height</c> that <c>CreateSandboxV2</c> writes into
        /// <c>Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity</c>
        /// (Scripts/Editor/CreateSandboxV2.cs). It is <c>TerrainData.size.y</c> for every tile in that
        /// scene, so it feeds real generation.
        /// </para>
        /// <para>
        /// It DISAGREES with <see cref="DefaultVerticalSpanMeters"/> by 3000 m (4000 vs 7000). By the node's
        /// own guard the sandbox scene is therefore rendering geology authored across a 7000 m window into
        /// 4000 m of terrain - a uniform 1.75x vertical compression of every slope in that scene. The
        /// literal is preserved here byte-identically rather than "fixed", because raising it to 7000 would
        /// change generated geometry, and which of the two numbers is right is the owner's vertical-extent
        /// decision. It lives in this file so both numbers are visible side by side in one place.
        /// </para>
        /// </summary>
        public const float SandboxV2AuthoredTerrainHeightMeters = 4000f;
    }
}
