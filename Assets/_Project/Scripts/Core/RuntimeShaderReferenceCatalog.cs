using UnityEngine;

namespace Hecton8.Core
{
    [CreateAssetMenu(fileName = ResourceName, menuName = "Hecton8/Core/Runtime Shader Reference Catalog")]
    public sealed class RuntimeShaderReferenceCatalog : ScriptableObject
    {
        private const string ResourceName = "RuntimeShaderReferenceCatalog";

        private static RuntimeShaderReferenceCatalog s_cachedCatalog;

        [SerializeField] private Shader flexiblePipeShader;
        [SerializeField] private Shader sceneTransitionDitherShader;
        [SerializeField] private Shader groundRadarPingIndirectShader;
        [SerializeField] private Shader plasmaBeamIndirectShader;
        [SerializeField] private Material plasmaBeamIndirectMaterial;
        [SerializeField] private Shader geologyImpostorBillboardShader;
        [SerializeField] private Shader abyssalSsdoShader;
        [SerializeField] private Shader stochasticSsrShader;
        [SerializeField] private Shader noirDepthFogShader;
        [SerializeField] private Shader halfResParticleCompositeShader;
        [SerializeField] private Shader volumetricFogDearLieProxyShader;
        [SerializeField] private Shader volumetricLightProxyShader;
        [SerializeField] private Shader radarBlipInstancedShader;
        [SerializeField] private Shader scooterVolumetricShaftsShader;
        [SerializeField] private Shader runtimeFlatColorShader;
        [SerializeField] private Shader voxelBakeGhostShader;
        [SerializeField] private Shader droneFleetProceduralShader;
        [SerializeField] private Material droneFleetProceduralMaterial;
        [SerializeField] private Shader wreckIndirectLitShader;
        [SerializeField] private Shader marauderOutpostIndirectShader;
        [SerializeField] private Shader carveDebrisIndirectShader;
        [SerializeField] private Shader runtimeCheckerboardUnlitShader;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_cachedCatalog = null;
        }

        public static void Register(RuntimeShaderReferenceCatalog catalog)
        {
            if (catalog != null)
                s_cachedCatalog = catalog;
        }

        public static void Unregister(RuntimeShaderReferenceCatalog catalog)
        {
            if (ReferenceEquals(s_cachedCatalog, catalog))
                s_cachedCatalog = null;
        }

        public static bool TryGetFlexiblePipeShader(out Shader shader)
        {
            RuntimeShaderReferenceCatalog catalog = s_cachedCatalog;
            shader = catalog != null ? catalog.flexiblePipeShader : null;
            return shader != null;
        }

        public static bool TryGetSceneTransitionDitherShader(out Shader shader)
        {
            RuntimeShaderReferenceCatalog catalog = s_cachedCatalog;
            shader = catalog != null ? catalog.sceneTransitionDitherShader : null;
            return shader != null;
        }

        public static bool TryGetGroundRadarPingIndirectShader(out Shader shader)
        {
            RuntimeShaderReferenceCatalog catalog = s_cachedCatalog;
            shader = catalog != null ? catalog.groundRadarPingIndirectShader : null;
            return shader != null;
        }

        public static bool TryGetPlasmaBeamIndirectShader(out Shader shader)
        {
            RuntimeShaderReferenceCatalog catalog = s_cachedCatalog;
            shader = catalog != null ? catalog.plasmaBeamIndirectShader : null;
            return shader != null;
        }

        public static bool TryGetPlasmaBeamIndirectMaterial(out Material material)
        {
            RuntimeShaderReferenceCatalog catalog = s_cachedCatalog;
            material = catalog != null ? catalog.plasmaBeamIndirectMaterial : null;
            return material != null;
        }

        public static bool TryGetGeologyImpostorBillboardShader(out Shader shader)
        {
            RuntimeShaderReferenceCatalog catalog = s_cachedCatalog;
            shader = catalog != null ? catalog.geologyImpostorBillboardShader : null;
            return shader != null;
        }

        public static bool TryGetAbyssalSsdoShader(out Shader shader)
        {
            RuntimeShaderReferenceCatalog catalog = s_cachedCatalog;
            shader = catalog != null ? catalog.abyssalSsdoShader : null;
            return shader != null;
        }

        public static bool TryGetStochasticSsrShader(out Shader shader)
        {
            RuntimeShaderReferenceCatalog catalog = s_cachedCatalog;
            shader = catalog != null ? catalog.stochasticSsrShader : null;
            return shader != null;
        }

        public static bool TryGetNoirDepthFogShader(out Shader shader)
        {
            RuntimeShaderReferenceCatalog catalog = s_cachedCatalog;
            shader = catalog != null ? catalog.noirDepthFogShader : null;
            return shader != null;
        }

        public static bool TryGetHalfResParticleCompositeShader(out Shader shader)
        {
            RuntimeShaderReferenceCatalog catalog = s_cachedCatalog;
            shader = catalog != null ? catalog.halfResParticleCompositeShader : null;
            return shader != null;
        }

        public static bool TryGetVolumetricFogDearLieProxyShader(out Shader shader)
        {
            RuntimeShaderReferenceCatalog catalog = s_cachedCatalog;
            shader = catalog != null ? catalog.volumetricFogDearLieProxyShader : null;
            return shader != null;
        }

        public static bool TryGetVolumetricLightProxyShader(out Shader shader)
        {
            RuntimeShaderReferenceCatalog catalog = s_cachedCatalog;
            shader = catalog != null ? catalog.volumetricLightProxyShader : null;
            return shader != null;
        }

        public static bool TryGetRadarBlipInstancedShader(out Shader shader)
        {
            RuntimeShaderReferenceCatalog catalog = s_cachedCatalog;
            shader = catalog != null ? catalog.radarBlipInstancedShader : null;
            return shader != null;
        }

        public static bool TryGetScooterVolumetricShaftsShader(out Shader shader)
        {
            RuntimeShaderReferenceCatalog catalog = s_cachedCatalog;
            shader = catalog != null ? catalog.scooterVolumetricShaftsShader : null;
            return shader != null;
        }

        public static bool TryGetRuntimeFlatColorShader(out Shader shader)
        {
            RuntimeShaderReferenceCatalog catalog = s_cachedCatalog;
            shader = catalog != null ? catalog.runtimeFlatColorShader : null;
            return shader != null;
        }

        public static bool TryGetVoxelBakeGhostShader(out Shader shader)
        {
            RuntimeShaderReferenceCatalog catalog = s_cachedCatalog;
            shader = catalog != null ? catalog.voxelBakeGhostShader : null;
            return shader != null;
        }

        public static bool TryGetDroneFleetProceduralShader(out Shader shader)
        {
            RuntimeShaderReferenceCatalog catalog = s_cachedCatalog;
            shader = catalog != null ? catalog.droneFleetProceduralShader : null;
            return shader != null;
        }

        public static bool TryGetDroneFleetProceduralMaterial(out Material material)
        {
            RuntimeShaderReferenceCatalog catalog = s_cachedCatalog;
            material = catalog != null ? catalog.droneFleetProceduralMaterial : null;
            return material != null;
        }

        public static bool TryGetWreckIndirectLitShader(out Shader shader)
        {
            RuntimeShaderReferenceCatalog catalog = s_cachedCatalog;
            shader = catalog != null ? catalog.wreckIndirectLitShader : null;
            return shader != null;
        }

        public static bool TryGetMarauderOutpostIndirectShader(out Shader shader)
        {
            RuntimeShaderReferenceCatalog catalog = s_cachedCatalog;
            shader = catalog != null ? catalog.marauderOutpostIndirectShader : null;
            return shader != null;
        }

        public static bool TryGetCarveDebrisIndirectShader(out Shader shader)
        {
            RuntimeShaderReferenceCatalog catalog = s_cachedCatalog;
            shader = catalog != null ? catalog.carveDebrisIndirectShader : null;
            return shader != null;
        }

        public static bool TryGetRuntimeCheckerboardUnlitShader(out Shader shader)
        {
            RuntimeShaderReferenceCatalog catalog = s_cachedCatalog;
            shader = catalog != null ? catalog.runtimeCheckerboardUnlitShader : null;
            return shader != null;
        }
    }
}
