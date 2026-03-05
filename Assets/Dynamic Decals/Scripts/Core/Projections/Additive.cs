using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LlockhamIndustries.Decals
{
    /**
    * Additive projection. Draws to the screen additively. If rendering in deferred, will be drawn in forward, after all other projections.
    */
    [System.Serializable]
    public class Additive : Base
    {
        public override Material[] Forward
        {
            get
            {
                if (forwardMaterials == null || forwardMaterials.Length != 1)
                {
                    forwardMaterials = new Material[1];
                }
                if (forwardMaterials[0] == null)
                {
                    forwardMaterials[0] = new Material(Shader.Find("Projection/Decal/Additive"));
                    forwardMaterials[0].hideFlags = HideFlags.DontSave;
                    Apply(forwardMaterials[0]);
                }
                return forwardMaterials;
            }
        }
    }
}
