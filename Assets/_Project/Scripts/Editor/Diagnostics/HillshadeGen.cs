using UnityEngine;
using System.IO;

public static class HillshadeGen {
    public static void Gen() {
        string inPath = @"C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-b333-42a8-ad13-119572c28fd0\heightmap_10km.png";
        string outPath = @"C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-b333-42a8-ad13-119572c28fd0\hillshade_eroded.png";
        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(File.ReadAllBytes(inPath));
        
        Texture2D hill = new Texture2D(tex.width, tex.height, TextureFormat.RGB24, false);
        Vector3 lightDir = new Vector3(-1f, 0.5f, 1f).normalized;
        float hScale = 12000f / 10000f; // roughly
        
        for (int y=1; y<tex.height-1; y++) {
            for (int x=1; x<tex.width-1; x++) {
                float hL = tex.GetPixel(x-1, y).r * hScale;
                float hR = tex.GetPixel(x+1, y).r * hScale;
                float hD = tex.GetPixel(x, y-1).r * hScale;
                float hU = tex.GetPixel(x, y+1).r * hScale;
                Vector3 n = new Vector3(hL - hR, 2f, hD - hU).normalized;
                float i = Mathf.Max(0f, Vector3.Dot(n, lightDir));
                hill.SetPixel(x, y, new Color(i,i,i,1f));
            }
        }
        File.WriteAllBytes(outPath, hill.EncodeToPNG());
        Debug.Log("Hillshade eroded done.");
    }
}
