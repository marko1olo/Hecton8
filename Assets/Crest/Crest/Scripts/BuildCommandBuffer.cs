// Crest Ocean System

// Copyright 2020 Wave Harmonic Ltd

using UnityEngine;

namespace Crest
{
    /// <summary>
    /// Base class for the command buffer builder, which takes care of updating all ocean-related data. If you wish to provide your
    /// own update logic, you can create a new component that inherits from this class and attach it to the same GameObject as the
    /// OceanRenderer script. The new component should be set to update after the Default bucket, similar to BuildCommandBuffer.
    /// </summary>
    public abstract class BuildCommandBufferBase
    {
        /// <summary>
        /// Used to validate update order
        /// </summary>
        public static int _lastUpdateFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void InitStatics()
        {
            // Init here from 2019.3 onwards
            _lastUpdateFrame = -1;
        }
    }

    /// <summary>
    /// The default builder for the ocean update command buffer which takes care of updating all ocean-related data, for
    /// example rendering animated waves and advancing sims. This runs in LateUpdate after the Default bucket, after the ocean
    /// system been moved to an up to date position and frame processing is done.
    /// </summary>
    public class BuildCommandBuffer : BuildCommandBufferBase
    {
        public static void FlipDataBuffers(OceanRenderer ocean)
        {
            var renderData = ocean._lodTransform._renderData;
            for (int i = 0; i < renderData.Length; i++)
            {
                renderData[i].Flip();
            }
        }

        /// <summary>
        /// Construct the command buffer and attach it to the camera so that it will be executed in the render.
        /// </summary>
        public void BuildAndExecute()
        {
            if (OceanRenderer.Instance == null) return;

            FlipDataBuffers(OceanRenderer.Instance);
            _lastUpdateFrame = OceanRenderer.FrameCount;
        }
    }
}
