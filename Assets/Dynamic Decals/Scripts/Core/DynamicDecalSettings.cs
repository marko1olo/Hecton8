using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using LlockhamIndustries.ExtensionMethods;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LlockhamIndustries.Decals
{
    public class DynamicDecalSettings : ScriptableObject
    {
        //Pools
        public PoolInstance[] pools;

        //Masking
        public ProjectionLayer[] Layers
        {
            get { return layers; }
            set
            {
                if (layers != value)
                {
                    layers = value;
                    CalculatePasses();
                }
            }
        }
        public List<ReplacementPass> Passes
        {
            get { return passes; }
        }

        //VR
        public bool SinglePassVR
        {
            get { return singlepassVR; }
        }

        //Backing fields
        [SerializeField]
        private ProjectionLayer[] layers;
        [SerializeField]
        private List<ReplacementPass> passes;
        [SerializeField]
        private bool singlepassVR;

        //Constructor
        public DynamicDecalSettings()
        {
            //Initialize pools with default values
            ResetPools();

            //Initialize masking with default values
            ResetMasking();
        }

        //Reset
        public void ResetSettings()
        {
            //Update renderers
            DynamicDecals.System.UpdateRenderers();
        }
        public void ResetPools()
        {
            //Reset pools to default values
            pools = new PoolInstance[] { new PoolInstance("Default", null) };
        }
        public void ResetMasking()
        {
            layers = new ProjectionLayer[] { new ProjectionLayer("Layer 1"), new ProjectionLayer("Layer 2"), new ProjectionLayer("Layer 3"), new ProjectionLayer("Layer 4") };
            CalculatePasses();
        }

        //Pass calculation
        public void CalculatePasses()
        {
            //Initialize / clear passes
            if (passes == null) passes = new List<ReplacementPass>();
            else passes.Clear();

            for (int i = 0; i < 32; i++)
            {
                //Generate layer vector
                Vector4 layerVector = LayerVector(i);

                //Add to passes
                AddToPasses(i, layerVector);
            }
        }

        private Vector4 LayerVector (int LayerIndex)
        {
            Vector4 vector = new Vector4(0, 0, 0, 0);

            if (layers[0].layers.Contains(LayerIndex)) vector.x = 1;
            if (layers[1].layers.Contains(LayerIndex)) vector.y = 1;
            if (layers[2].layers.Contains(LayerIndex)) vector.z = 1;
            if (layers[3].layers.Contains(LayerIndex)) vector.w = 1;

            return vector;
        }
        private void AddToPasses(int LayerIndex, Vector4 LayerVector)
        {
            //Check if we can be added to an existing pass
            for (int i = 0; i < passes.Count; i++)
            {
                if (passes[i].vector == LayerVector)
                {
                    passes[i].layers = passes[i].layers.Add(LayerIndex);
                    return;
                }
            }

            //Create a new pass with the current layer vector
            passes.Add(new ReplacementPass(LayerIndex, LayerVector));
        }

        //VR calculation
        public void CalculateVR()
        {
            #if UNITY_EDITOR
            singlepassVR = PlayerSettings.stereoRenderingPath != StereoRenderingPath.MultiPass;
            #endif
        }
    }

    public enum ShaderReplacement { Null, SingleTarget, SinglePass, DoublePass, TriplePass, Classic };

    [System.Serializable]
    public class PoolInstance
    {
        public int id;
        public string title;
        public int[] limits;

        public PoolInstance(string Title, PoolInstance[] CurrentInstances)
        {
            id = UniqueID(CurrentInstances);
            title = Title;

            //15 Quality Settings maximum
            limits = new int[15];
            //Set all defaults
            for (int i = 0; i < limits.Length; i++)
            {
                limits[i] = ((i + 1) * 400);
            }
        }
        private int UniqueID(PoolInstance[] CurrentInstances)
        {
            //We use an ID instead of a name or an index to keep track of our pool as it allows us to rename and reorder pools while maintaining a hidden reference to them. 
            //Also lookup from a dictionary is faster than iterating over all pools for a given name.

            //Start at 0 (1 if not the first) and iterate upwards until we have an ID not currently in use.
            int ID = 0;
            bool Unique = false;

            if (CurrentInstances != null)
            {
                while (!Unique)
                {
                    //ID, wan't unique. Increment and check again.
                    ID++;
                    Unique = true;
                    //Start unique as true, then iterate over all instances to see if its otherwise.
                    for (int i = 0; i < CurrentInstances.Length; i++)
                    {
                        if (CurrentInstances[i] != null && ID == CurrentInstances[i].id) Unique = false;
                    }
                }
            }

            //We have a unique ID! System falls apart if we have more than 2,147,483,647 pools at once. Seems unlikely.
            return ID;
        }
    }

    [System.Serializable]
    public struct ProjectionLayer
    {
        public string name;
        public LayerMask layers;

        public ProjectionLayer(string Name)
        {
            name = Name;
            layers = 0;
        }
        public ProjectionLayer(string Name, int Layer)
        {
            name = Name;
            layers = (1 << Layer);
        }
        public ProjectionLayer(string Name, LayerMask Layers)
        {
            name = Name;
            layers = Layers;
        }
    }

    [System.Serializable]
    public class ReplacementPass
    {
        public Vector4 vector;
        public LayerMask layers;

        public ReplacementPass(int LayerIndex, Vector4 LayerVector)
        {
            vector = LayerVector;
            layers = (1 << LayerIndex);
        }
    }
}