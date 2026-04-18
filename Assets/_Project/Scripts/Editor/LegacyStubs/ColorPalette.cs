using System;
using UnityEngine;

namespace UnityEditor.Polybrush
{
    /// <summary>
    /// Legacy compatibility stub for abandoned Polybrush color palette assets.
    /// </summary>
    public sealed class ColorPalette : ScriptableObject
    {
        public Color current = Color.white;
        public Color[] colors = Array.Empty<Color>();
    }
}
