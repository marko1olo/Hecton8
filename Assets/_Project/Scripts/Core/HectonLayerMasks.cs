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
        /// <summary>Unity's serialized Everything LayerMask value. Forbidden in project-authored data.</summary>
        public const int EverythingLayerMaskValue = -1;

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

        /// <summary>Compile-time unsigned mask for all populated project layers.</summary>
        public const uint AllDefinedProjectRenderingLayerMaskValue =
            (1u << 23) - 1u;

        /// <summary>Compile-time terrain/SDF host mask for resource-node placement authoring.</summary>
        public const int TerrainSdfProbeLayerMaskValue =
            (1 << 7) |
            (1 << 12) |
            (1 << 21);

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

        /// <summary>Brine toxicity gameplay layer index.</summary>
        public static readonly int BrineToxicity = 20;

        /// <summary>Async voxel proxy collider layer index.</summary>
        public static readonly int VoxelProxy = 21;

        /// <summary>Combined player vehicle collision layer index.</summary>
        public static readonly int PlayerVehicle = 22;

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

        /// <summary>Brine toxicity collision/query mask.</summary>
        public static readonly int BrineToxicityLayerMask = 1 << BrineToxicity;

        /// <summary>Async voxel proxy collision/query mask.</summary>
        public static readonly int VoxelProxyLayerMask = 1 << VoxelProxy;

        /// <summary>Combined player vehicle collision/query mask.</summary>
        public static readonly int PlayerVehicleLayerMask = 1 << PlayerVehicle;

        /// <summary>Strict base-building surface mask used by construction raycasts.</summary>
        public static readonly int ConstructionSurfaceLayerMask =
            BaseModuleLayerMask |
            DroppedItemLayerMask |
            CreatureLayerMask;

        /// <summary>World collision mask used by seam-probe ray batches.</summary>
        public static readonly int SeamProbeLayerMask =
            (TerrainLayerMask |
             BaseModuleLayerMask |
             VehicleLayerMask |
             VoxelCaveLayerMask |
             VoxelProxyLayerMask |
             DebrisLayerMask) &
            ~UILayerMask &
            ~IgnoreRaycastLayerMask;

        /// <summary>Terrain/SDF surface layers accepted by ground, seabed, and snap probes.</summary>
        public static readonly int TerrainSdfProbeLayerMask = TerrainSdfProbeLayerMaskValue;

        /// <summary>Mounted-player sweep mask for vehicle/structure obstruction probes.</summary>
        public static readonly int MountedSweepLayerMask =
            DefaultLayerMask |
            InteractableLayerMask |
            TerrainLayerMask |
            StrictInteractionLayerMask |
            VehicleLayerMask |
            VoxelCaveLayerMask |
            VoxelProxyLayerMask |
            DebrisLayerMask;

        /// <summary>Primary handheld tool surface target mask, excluding player, water, UI, presentation, and authoring-only layers.</summary>
        public static readonly int FieldToolSurfaceLayerMask = MountedSweepLayerMask;

        /// <summary>Scanner/analyzer target mask, extending physical surfaces with trigger and brine hazard zones.</summary>
        public static readonly int FieldToolScanLayerMask =
            FieldToolSurfaceLayerMask |
            TriggerZoneLayerMask |
            BrineToxicityLayerMask;

        /// <summary>All populated project layers, capped to the explicitly authored project layer set.</summary>
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
            (1 << 19) |
            (1 << 20) |
            (1 << 21) |
            (1 << 22);

        /// <summary>All populated project layers except Ignore Raycast.</summary>
        public static readonly int DefaultRaycastLayerMask =
            AllDefinedProjectLayersMask &
            ~IgnoreRaycastLayerMask &
            ~VoxelProxyLayerMask;

        /// <summary>Scene/render graph world layers, excluding UI and post-process presentation layers.</summary>
        public static readonly int RenderGraphWorldLayerMask =
            AllDefinedProjectLayersMask &
            ~UILayerMask &
            ~HudInternalLayerMask &
            ~PostProcessLayerMask &
            ~VoxelProxyLayerMask;

        /// <summary>Unsigned mask for all populated project rendering layers.</summary>
        public static readonly uint AllDefinedProjectRenderingLayerMask = AllDefinedProjectRenderingLayerMaskValue;

        /// <summary>Returns true when a serialized Unity LayerMask represents Everything.</summary>
        public static bool IsEverythingLayerMask(int layerMask)
        {
            return layerMask == EverythingLayerMaskValue;
        }

        /// <summary>Returns an authoring-safe mask, replacing Everything with the explicit project layer set.</summary>
        public static int SanitizeAuthoringLayerMask(int layerMask)
        {
            return layerMask < 0 ? AllDefinedProjectLayersMask : layerMask;
        }

        /// <summary>Resolves serialized probe masks so legacy strict/empty settings still hit terrain and voxel surfaces.</summary>
        public static int ResolveTerrainSdfProbeLayerMask(int layerMask)
        {
            if (layerMask == 0 || IsEverythingLayerMask(layerMask))
                return TerrainSdfProbeLayerMask;

            int sanitizedMask = SanitizeAuthoringLayerMask(layerMask);
            if (layerMask == StrictInteractionLayerMask)
                return sanitizedMask | TerrainSdfProbeLayerMask;

            return IncludesAnyLayer(sanitizedMask, TerrainSdfProbeLayerMask)
                ? sanitizedMask | TerrainSdfProbeLayerMask
                : sanitizedMask;
        }

        /// <summary>Resolves resource-node host layers; legacy strict masks become terrain/SDF hosts.</summary>
        public static int ResolveResourceNodeHostLayerMask(int layerMask)
        {
            if (layerMask == 0 ||
                IsEverythingLayerMask(layerMask) ||
                layerMask == StrictInteractionLayerMask)
            {
                return TerrainSdfProbeLayerMask;
            }

            int sanitizedMask = SanitizeAuthoringLayerMask(layerMask);
            return IncludesAnyLayer(sanitizedMask, TerrainSdfProbeLayerMask)
                ? sanitizedMask | TerrainSdfProbeLayerMask
                : sanitizedMask;
        }

        /// <summary>Resolves tool surface masks while preserving explicit broad/custom masks.</summary>
        public static int ResolveSurfaceInteractionLayerMask(int layerMask)
        {
            if (layerMask == 0)
                return 0;

            int sanitizedMask = SanitizeAuthoringLayerMask(layerMask);
            if (layerMask == StrictInteractionLayerMask)
                return sanitizedMask | TerrainSdfProbeLayerMask;

            return IncludesAnyLayer(sanitizedMask, TerrainSdfProbeLayerMask)
                ? sanitizedMask | TerrainSdfProbeLayerMask
                : sanitizedMask;
        }

        /// <summary>Resolves scanner/advice masks; legacy strict/everything authoring becomes the full field scan lane.</summary>
        public static int ResolveFieldToolScanLayerMask(int layerMask)
        {
            if (layerMask == 0)
                return 0;

            if (layerMask == StrictInteractionLayerMask || IsEverythingLayerMask(layerMask))
                return FieldToolScanLayerMask;

            return ResolveSurfaceInteractionLayerMask(layerMask);
        }

        /// <summary>Maps an int layer mask to the renderer-safe unsigned payload used by GPU draw APIs.</summary>
        public static uint ToRenderingLayerMask(int layerMask)
        {
            return (uint)SanitizeAuthoringLayerMask(layerMask) & AllDefinedProjectRenderingLayerMaskValue;
        }

        private static bool IncludesAnyLayer(int layerMask, int requiredLayers)
        {
            return (layerMask & requiredLayers) != 0;
        }
    }
}
