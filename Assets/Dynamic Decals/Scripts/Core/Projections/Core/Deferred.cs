using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LlockhamIndustries.Decals
{
    /**
    * The base of all deferred only projections (Gloss, Normal)
    */
    [System.Serializable]
    public abstract class Deferred : Projection
    {
        //Deferred only
        public override bool DeferredOnly
        {
            get { return true; }
        }

        //Unsupported materials
        public override Material[] Forward
        {
            get { return null; }
        }
    }
}