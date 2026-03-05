using UnityEngine;
using System.Collections;
using System;

namespace LlockhamIndustries.Decals
{
    /**
    * Deferred Only normal projection. Only affects the normal buffer. Useful for adding cracks or normal details to tiled surfaces.
    */
    [System.Serializable]
    public class Normal : Deferred
    {
        //Materials
        public override Material[] DeferredOpaque
        {
            get { return DeferredMaterials; }
        }
        public override Material[] DeferredTransparent
        {
            get { return DeferredMaterials; }
        }
        private Material[] DeferredMaterials
        {
            get
            {
                if (deferredMaterials == null || deferredMaterials.Length != 1)
                {
                    deferredMaterials = new Material[1];
                }
                if (deferredMaterials[0] == null)
                {
                    deferredMaterials[0] = new Material(Shader.Find("Projection/Decal/Normal"));
                    deferredMaterials[0].hideFlags = HideFlags.DontSave;
                    Apply(deferredMaterials[0]);
                }
                return deferredMaterials;
            }
        }

        //Instanced count
        public override int InstanceLimit
        {
            get { return 500; }
        }

        protected override void UpdateMaterials()
        {
            UpdateMaterialArray(deferredMaterials);
        }
        protected override void Apply(Material Material)
        {
            //Apply base
            base.Apply(Material);

            //Apply shape
            shape.Apply(Material);

            //Apply normal
            normal.Apply(Material);
        }

        protected override void DestroyMaterials()
        {
            if (deferredMaterials != null)
            {
                DestroyMaterialArray(deferredMaterials);
            }
        }

        //Static Properties
        /**
        * The shape of your projection.
        * The r channel of these properties is used to determine the projections transparency.
        */
        public ShapePropertyGroup shape;
        /**
        * The primary color details of your projection.
        * The alpha channel of these properties is used to determine the projections transparency.
        */
        public NormalPropertyGroup normal;

        protected override void OnEnable()
        {
            //Initialize our property groups
            if (shape == null) shape = new ShapePropertyGroup(this);
            if (normal == null) normal = new NormalPropertyGroup(this);

            base.OnEnable();
        }
        protected override void GenerateIDs()
        {
            base.GenerateIDs();

            shape.GenerateIDs();
            normal.GenerateIDs();
        }

        //Instanced Properties
        public override void UpdateProperties()
        {
            //Initialize property array
            if (properties == null || properties.Length != 2) properties = new ProjectionProperty[2];

            //Shape Modifier
            properties[0] = new ProjectionProperty("Opacity", shape._Multiplier, shape.Multiplier);

            //Normal Strength
            properties[1] = new ProjectionProperty("Normal Strength", normal._BumpScale, normal.Strength);
        }

        //Materials
        protected Material[] deferredMaterials;
    }
}
