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
    * Standard Shader - specular setup.
    */
    [System.Serializable]
    public class Specular : Projection
    {
        //Materials
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
                    forwardMaterials[0] = new Material(Shader.Find("Projection/Decal/Specular/Forward"));
                    forwardMaterials[0].hideFlags = HideFlags.DontSave;
                    Apply(forwardMaterials[0]);
                }
                return forwardMaterials;
            }
        }
        public override Material[] DeferredOpaque
        {
            get
            {
                if (deferredOpaqueMaterials == null || deferredOpaqueMaterials.Length != 1)
                {
                    deferredOpaqueMaterials = new Material[1];
                }
                if (deferredOpaqueMaterials[0] == null)
                {
                    deferredOpaqueMaterials[0] = new Material(Shader.Find("Projection/Decal/Specular/DeferredOpaque"));
                    deferredOpaqueMaterials[0].hideFlags = HideFlags.DontSave;
                    Apply(deferredOpaqueMaterials[0]);
                }
                return deferredOpaqueMaterials;
            }
        }
        public override Material[] DeferredTransparent
        {
            get
            {
                if (deferredTransparentMaterials == null || deferredTransparentMaterials.Length != 2)
                {
                    deferredTransparentMaterials = new Material[2];
                }
                if (deferredTransparentMaterials[0] == null)
                {
                    deferredTransparentMaterials[0] = new Material(Shader.Find("Projection/Decal/Specular/DeferredBaseTransparent"));
                    deferredTransparentMaterials[0].hideFlags = HideFlags.DontSave;
                    Apply(deferredTransparentMaterials[0]);
                }
                if (deferredTransparentMaterials[1] == null)
                {
                    deferredTransparentMaterials[1] = new Material(Shader.Find("Projection/Decal/Specular/DeferredAddTransparent"));
                    deferredTransparentMaterials[1].hideFlags = HideFlags.DontSave;
                    Apply(deferredTransparentMaterials[1]);
                }
                return deferredTransparentMaterials;
            }
        }

        //Instanced Count
        public override int InstanceLimit
        {
            get { return 500; }
        }

        protected override void UpdateMaterials()
        {
            UpdateMaterialArray(forwardMaterials);
            UpdateMaterialArray(deferredOpaqueMaterials);
            UpdateMaterialArray(deferredTransparentMaterials);
        }
        protected override void Apply(Material Material)
        {
            //Apply base
            base.Apply(Material);

            //Apply metallic
            albedo.Apply(Material);
            specular.Apply(Material);
            normal.Apply(Material);
            emissive.Apply(Material);
        }

        protected override void DestroyMaterials()
        {
            if (forwardMaterials != null)
            {
                DestroyMaterialArray(forwardMaterials);
            }
            if (deferredOpaqueMaterials != null)
            {
                DestroyMaterialArray(deferredOpaqueMaterials);
            }
            if (deferredTransparentMaterials != null)
            {
                DestroyMaterialArray(deferredTransparentMaterials);
            }
        }

        //Static Properties
        /**
        * The primary color details of your projection.
        * The alpha channel of these properties is used to determine the projections transparency.
        */
        public AlbedoPropertyGroup albedo;
        /**
        * The specular texture, with a color multiplier.
        * Determines how the light bouncing off the surface of the decal appears.
        * The color defines the color of the light that reflects of your surface.
        * The alpha defines how glossy your surface appears.
        */
        public SpecularPropertyGroup specular;
        /**
        * The normal texture of your decal, multiplied by the normal strength. 
        * Normals determine how the surface of your decal interacts with lights.
        */
        public NormalPropertyGroup normal;
        /**
        * The emission texture of your projection, multiplied by the emission color and intensity.
        * Emission allows us to make a decal appear as if it's emitting light. Supports HDR.
        */
        public EmissivePropertyGroup emissive;

        protected override void OnEnable()
        {
            //Initialize our property groups
            if (albedo == null) albedo = new AlbedoPropertyGroup(this);  
            if (specular == null) specular = new SpecularPropertyGroup(this);
            if (normal == null) normal = new NormalPropertyGroup(this);
            if (emissive == null) emissive = new EmissivePropertyGroup(this);

            base.OnEnable();
        }
        protected override void GenerateIDs()
        {
            base.GenerateIDs();

            albedo.GenerateIDs();
            specular.GenerateIDs();
            normal.GenerateIDs();
            emissive.GenerateIDs();
        }

        //Instanced Properties
        public override void UpdateProperties()
        {
            //Initialize property array
            if (properties == null || properties.Length != 2) properties = new ProjectionProperty[2];

            //Albedo Color
            properties[0] = new ProjectionProperty("Albedo", albedo._Color, albedo.Color);

            //Emission Color
            properties[1] = new ProjectionProperty("Emission", emissive._EmissionColor, emissive.Color, emissive.Intensity);
        }

        //Materials
        protected Material[] forwardMaterials;
        protected Material[] deferredOpaqueMaterials;
        protected Material[] deferredTransparentMaterials;
    }
}
