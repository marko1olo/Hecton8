using System.Collections.Generic;

namespace MapMagic.Brush
{
    public partial class MapMagicBrush
    {
        public static readonly HashSet<MapMagicBrush> Instances = new HashSet<MapMagicBrush>();

        private void Awake()
        {
            Instances.Add(this);
        }

        private void OnDestroy()
        {
            Instances.Remove(this);
        }
    }
}
