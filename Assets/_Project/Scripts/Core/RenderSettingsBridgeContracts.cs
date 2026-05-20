using UnityEngine;

namespace Hecton8.Core
{
    public interface IAtmosphereRenderSettingsBridge : ISystem
    {
        Material Skybox { get; }

        bool SetSkybox(Material material);
    }
}
