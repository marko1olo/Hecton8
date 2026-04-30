namespace Hecton8.Core
{
    /// <summary>
    /// Central audited Unity layer indices and masks for HECTON-8.
    /// </summary>
    /// <remarks>
    /// Layer indices are sourced from ProjectSettings/TagManager.asset.
    /// Keep this file synchronized with TagManager changes; do not scatter layer
    /// literals or layer-name lookups through runtime code.
    /// </remarks>
    public static class HectonLayerMasks
    {
        /// <summary>Compile-time strict authoring mask value for optional parameter defaults.</summary>
        public const int DataTemplateAuthoringMaskValue =
            (1 << 8) |
            (1 << 9) |
            (1 << 10);

        /// <summary>Compile-time rendering-layer form of <see cref="DataTemplateAuthoringMaskValue"/>.</summary>
        public const uint DataTemplateAuthoringRenderingMaskValue =
            (1u << 8) |
            (1u << 9) |
            (1u << 10);

        /// <summary>Compile-time 20-bit renderer-safe all-project-layers mask.</summary>
        public const uint AllDefinedProjectRenderingLayerMaskValue =
            (1u << 20) - 1u;

        /// <summary>No Unity layers.</summary>
        public static readonly int NoLayers = 0;

        /// <summary>Default Unity layer index.</summary>
        public static readonly int Default = 0;

        /// <summary>TransparentFX Unity layer index.</summary>
        public static readonly int TransparentFX = 1;

        /// <summary>Ignore Raycast Unity layer index.</summary>
        public static readonly int IgnoreRaycast = 2;

        /// <summary>Interactable gameplay layer index.</summary>
        public static readonly int Interactable = 3;

        /// <summary>Water gameplay/rendering layer index.</summary>
        public static readonly int Water = 4;

        /// <summary>UI layer index.</summary>
        public static readonly int UI = 5;

        /// <summary>Player gameplay layer index.</summary>
        public static readonly int Player = 6;

        /// <summary>Terrain gameplay layer index.</summary>
        public static readonly int Terrain = 7;

        /// <summary>Base module gameplay layer index.</summary>
        public static readonly int BaseModule = 8;

        /// <summary>Dropped item gameplay layer index.</summary>
        public static readonly int DroppedItem = 9;

        /// <summary>Creature gameplay layer index.</summary>
        public static readonly int Creature = 10;

        /// <summary>Vehicle gameplay layer index.</summary>
        public static readonly int Vehicle = 11;

        /// <summary>Voxel cave gameplay layer index.</summary>
        public static readonly int VoxelCave = 12;

        /// <summary>Trigger zone layer index.</summary>
        public static readonly int TriggerZone = 13;

        /// <summary>Debris gameplay layer index.</summary>
        public static readonly int Debris = 14;

        /// <summary>Celestial presentation layer index.</summary>
        public static readonly int Celestial = 15;

        /// <summary>Post-processing volume layer index.</summary>
        public static readonly int PostProcess = 16;

        /// <summary>Internal HUD rendering layer index.</summary>
        public static readonly int HudInternal = 17;

        /// <summary>First-person tools layer index.</summary>
        public static readonly int FirstPersonTools = 18;

        /// <summary>Construction socket layer index.</summary>
        public static readonly int Sockets = 19;

        /// <summary>Strict authoring mask requested for data-template sanitation.</summary>
        public static readonly int DataTemplateAuthoringMask = DataTemplateAuthoringMaskValue;

        /// <summary>Rendering-layer form of <see cref="DataTemplateAuthoringMask"/>.</summary>
        public static readonly uint DataTemplateAuthoringRenderingMask = DataTemplateAuthoringRenderingMaskValue;

        /// <summary>Strict core gameplay query mask: BaseModule, DroppedItem, Creature.</summary>
        public static readonly int StrictInteractionLayerMask = DataTemplateAuthoringMaskValue;

        /// <summary>Rendering-layer form of <see cref="StrictInteractionLayerMask"/>.</summary>
        public static readonly uint StrictInteractionRenderingLayerMask = DataTemplateAuthoringRenderingMaskValue;

        /// <summary>Default layer collision/query mask.</summary>
        public static readonly int DefaultLayerMask = 1 << Default;

        /// <summary>TransparentFX layer collision/query mask.</summary>
        public static readonly int TransparentFxLayerMask = 1 << TransparentFX;

        /// <summary>Ignore Raycast layer collision/query mask.</summary>
        public static readonly int IgnoreRaycastLayerMask = 1 << IgnoreRaycast;

        /// <summary>Interactable layer collision/query mask.</summary>
        public static readonly int InteractableLayerMask = 1 << Interactable;

        /// <summary>Water layer collision/query mask.</summary>
        public static readonly int WaterLayerMask = 1 << Water;

        /// <summary>UI layer collision/query mask.</summary>
        public static readonly int UILayerMask = 1 << UI;

        /// <summary>Player layer collision/query mask.</summary>
        public static readonly int PlayerLayerMask = 1 << Player;

        /// <summary>Base module collision/query mask.</summary>
        public static readonly int BaseModuleLayerMask = 1 << BaseModule;

        /// <summary>Dropped item collision/query mask.</summary>
        public static readonly int DroppedItemLayerMask = 1 << DroppedItem;

        /// <summary>Creature collision/query mask.</summary>
        public static readonly int CreatureLayerMask = 1 << Creature;

        /// <summary>Terrain collision/query mask.</summary>
        public static readonly int TerrainLayerMask = 1 << Terrain;

        /// <summary>Vehicle collision/query mask.</summary>
        public static readonly int VehicleLayerMask = 1 << Vehicle;

        /// <summary>Voxel cave collision/query mask.</summary>
        public static readonly int VoxelCaveLayerMask = 1 << VoxelCave;

        /// <summary>Trigger zone collision/query mask.</summary>
        public static readonly int TriggerZoneLayerMask = 1 << TriggerZone;

        /// <summary>Debris collision/query mask.</summary>
        public static readonly int DebrisLayerMask = 1 << Debris;

        /// <summary>Celestial presentation layer mask.</summary>
        public static readonly int CelestialLayerMask = 1 << Celestial;

        /// <summary>Post-processing volume layer mask.</summary>
        public static readonly int PostProcessLayerMask = 1 << PostProcess;

        /// <summary>Internal HUD rendering layer mask.</summary>
        public static readonly int HudInternalLayerMask = 1 << HudInternal;

        /// <summary>First-person tool presentation layer mask.</summary>
        public static readonly int FirstPersonToolsLayerMask = 1 << FirstPersonTools;

        /// <summary>Construction socket collision/query mask.</summary>
        public static readonly int SocketsLayerMask = 1 << Sockets;

        /// <summary>Strict base-building surface mask used by construction raycasts.</summary>
        public static readonly int ConstructionSurfaceLayerMask =
            BaseModuleLayerMask |
            DroppedItemLayerMask |
            CreatureLayerMask;

        /// <summary>World collision mask used by seam-probe ray batches.</summary>
        public static readonly int SeamProbeLayerMask =
            TerrainLayerMask |
            BaseModuleLayerMask |
            VehicleLayerMask |
            VoxelCaveLayerMask |
            DebrisLayerMask;

        /// <summary>Mounted-player sweep mask for vehicle/structure obstruction probes.</summary>
        public static readonly int MountedSweepLayerMask =
            DefaultLayerMask |
            InteractableLayerMask |
            TerrainLayerMask |
            StrictInteractionLayerMask |
            VehicleLayerMask |
            VoxelCaveLayerMask |
            DebrisLayerMask;

        /// <summary>All populated project layers, capped to Unity's 20-bit rendering-layer ceiling.</summary>
        public static readonly int AllDefinedProjectLayersMask =
            (1 << 0) |
            (1 << 1) |
            (1 << 2) |
            (1 << 3) |
            (1 << 4) |
            (1 << 5) |
            (1 << 6) |
            (1 << 7) |
            (1 << 8) |
            (1 << 9) |
            (1 << 10) |
            (1 << 11) |
            (1 << 12) |
            (1 << 13) |
            (1 << 14) |
            (1 << 15) |
            (1 << 16) |
            (1 << 17) |
            (1 << 18) |
            (1 << 19);

        /// <summary>All populated project layers except Ignore Raycast.</summary>
        public static readonly int DefaultRaycastLayerMask =
            AllDefinedProjectLayersMask & ~IgnoreRaycastLayerMask;

        /// <summary>Twenty-bit renderer-safe mask for all populated project rendering layers.</summary>
        public static readonly uint AllDefinedProjectRenderingLayerMask = AllDefinedProjectRenderingLayerMaskValue;
    }
}
