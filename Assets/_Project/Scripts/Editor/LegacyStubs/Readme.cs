using System;
using UnityEngine;

/// <summary>
/// Legacy compatibility stub for template readme assets left in the project.
/// </summary>
public sealed class Readme : ScriptableObject
{
    [Serializable]
    public struct Section
    {
        public string heading;
        public string text;
        public string linkText;
        public string url;
    }

    public Texture2D icon;
    public string title;
    public Section[] sections = Array.Empty<Section>();
    public bool loadedLayout;
}
