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
    /// <para>
    /// READ THIS BEFORE POINTING A DIAGNOSTIC AT <see cref="DefaultVerticalSpanMeters"/>. The
    /// <c>Default*</c> members are C# FIELD INITIALISERS. A MapMagic node that has been serialised into a
    /// <c>.asset</c> carries its OWN values and the deserialiser overwrites the initialiser, so the number
    /// that actually generates terrain is the one in the graph asset - never this one. Measured from the
    /// serialised bits of every graph in the repo (method on <see cref="LiveWorldGraphSpanMeters"/>):
    /// <list type="bullet">
    /// <item><description>
    /// <c>Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset</c>, the live world graph:
    /// <c>lowWorldY = -10000</c>, <c>highWorldY = +2000</c>, span <b>12000 m</b>.
    /// </description></item>
    /// <item><description>
    /// <c>Data/World/Archive/HECTON_PROCEDURAL_GEOLOGY_GRAPH.asset</c>:
    /// <c>lowWorldY = -5000</c>, <c>highWorldY = +1000</c>, span <b>6000 m</b>.
    /// </description></item>
    /// </list>
    /// The initialiser pair below (-5000 / +2000, span 7000) therefore matches NEITHER graph - it takes its
    /// low from one and its high from the other. It is retained byte-identically because it IS what the
    /// field initialisers say, and changing it would alter any node instance deserialised without those
    /// fields. But it must not be presented as the world's vertical extent: a diagnostic that interprets a
    /// heightmap against 7000 m while the graph generated it against 12000 m is wrong by 1.714x. Six
    /// diagnostics tools hardcode <c>12000f</c> today; they AGREE with the live graph, and consolidating
    /// them must point at <see cref="LiveWorldGraphSpanMeters"/>, never at
    /// <see cref="DefaultVerticalSpanMeters"/>.
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

        /// <summary>
        /// Bottom of the window in the graph the WORLD actually generates from: -10000 m.
        /// <para>
        /// Read out of the serialised asset, not out of a comment or a field initialiser. MapMagic stores
        /// node fields as a parallel <c>fields:</c> / <c>values:</c> pair where each value is the raw
        /// IEEE-754 bit pattern of the float in a <c>v:</c> integer, so this cannot be read with a text
        /// search for the number and no tool in this repo could report it until it was decoded. Method,
        /// reproducible without Unity, without a GPU and without the Editor - which is the point, because
        /// every instrument that could have reported this number instead ran under conditions that make it
        /// return zeros:
        /// </para>
        /// <para>
        /// In <c>HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset</c>, locate the entry whose <c>type:</c> is
        /// <c>HectonSandboxAbyssalShelfMapMagicNode</c>, zip its <c>fields:</c> name list against its
        /// <c>values:</c> list positionally, then reinterpret the <c>v:</c> integer of each wanted name as a
        /// little-endian float. <c>highWorldY</c> = <c>1157234688</c> = <c>0x44FA0000</c> = <c>+2000.0</c>;
        /// <c>lowWorldY</c> = <c>3323740160</c> = <c>0xC61C4000</c> = <c>-10000.0</c>.
        /// </para>
        /// </summary>
        public const float LiveWorldGraphLowWorldY = -10000f;

        /// <summary>
        /// Top of the window in the graph the WORLD actually generates from: +2000 m. Decoded from
        /// <c>0x44FA0000</c>; see <see cref="LiveWorldGraphLowWorldY"/> for the method. This one happens to
        /// equal <see cref="DefaultHighWorldY"/>; the low does not, which is exactly why the pair cannot be
        /// assumed to match the initialisers.
        /// </summary>
        public const float LiveWorldGraphHighWorldY = 2000f;

        /// <summary>
        /// The span the live world graph normalises against: 12000 m. THIS is the number a diagnostic must
        /// use when it interprets a heightmap produced by the world graph, and it is the number the six
        /// height-dump tools already hardcode as a bare <c>12000f</c> literal.
        /// <para>
        /// It is a documented measurement of a serialised asset, not an authored decision, so it is NOT the
        /// value to change when the owner settles the vertical extent - changing the world means editing the
        /// graph node, and no tool in this repo can WRITE that node today
        /// (<c>HectonMacroGeologyBaseIntegrator.TryResolveSpanForMapMagicObject</c> only READS it). If this
        /// const ever disagrees with the asset, the asset is right and this const is stale: re-decode it.
        /// </para>
        /// </summary>
        public const float LiveWorldGraphSpanMeters = LiveWorldGraphHighWorldY - LiveWorldGraphLowWorldY;

        /// <summary>
        /// The span held by <c>Data/World/Archive/HECTON_PROCEDURAL_GEOLOGY_GRAPH.asset</c>: 6000 m
        /// (<c>lowWorldY = -5000</c>, <c>highWorldY = +1000</c>).
        /// <para>
        /// Recorded because that archived graph is still named by a hardcoded path in LIVE tools -
        /// <c>CreateSandboxV2.cs</c> and <c>UpdateSandboxSceneTask.cs</c> both load it from
        /// <c>Data/World/Sandbox/</c>, where it no longer is. So the sandbox scene's
        /// <see cref="SandboxV2AuthoredTerrainHeightMeters"/> of 4000 m was never a 7000/4000 = 1.75x
        /// compression against this file's initialisers; if that graph were reachable it would be
        /// 6000/4000 = 1.5x, and as things actually stand the graph resolves to null and the scene
        /// generates nothing at all.
        /// </para>
        /// </summary>
        public const float ArchivedProceduralGeologyGraphSpanMeters = 6000f;
    }
}
