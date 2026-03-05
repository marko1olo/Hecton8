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
    * Unlit projection. Draws a flat color to the screen. Useful for projected UI elements. If rendering in deferred, will be drawn in forward, after all other projections.
    */
    [System.Serializable]
    public class Unlit : Base
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
                    forwardMaterials[0] = new Material(Shader.Find("Projection/Decal/Unlit"));
                    forwardMaterials[0].hideFlags = HideFlags.DontSave;
                    Apply(forwardMaterials[0]);
                }
                return forwardMaterials;
            }
        }
    }
}
