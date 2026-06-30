using NUnit.Framework;
using Crest;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using System;

#if CREST_URP && UNITY_2023_3_OR_NEWER

namespace Crest.Tests
{
    public class CrestRenderGraphHelperTests
    {
        [Test]
        public void RenderGraphHelper_Handle_ReturnsNullWhenTextureHandleIsInvalid()
        {
            RenderGraphHelper.Handle handle = new RenderGraphHelper.Handle();

            // By default textureHandle is uninitialized/invalid.
            // Casting it to RTHandle should throw InvalidOperationException,
            // which should be caught and return null.
            RTHandle result = handle.RT;

            Assert.IsNull(result);
        }
    }
}

#endif
