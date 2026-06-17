using System;
using System.Drawing;
using System.IO;

class Program {
    static void Main() {
        string path = "C:\\Users\\danat\\.gemini\\antigravity\\brain\\389e4a53-b1e6-440c-b190-0f5c509fa8c4\\Terrain_View_B_0.png";
        if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }
        Bitmap bmp = new Bitmap(path);
        int magentaCount = 0;
        int totalPixels = bmp.Width * bmp.Height;
        for (int y = 0; y < bmp.Height; y++) {
            for (int x = 0; x < bmp.Width; x++) {
                Color c = bmp.GetPixel(x, y);
                if (c.R > 200 && c.G < 50 && c.B > 200) {
                    magentaCount++;
                }
            }
        }
        Console.WriteLine("Magenta pixels: " + magentaCount + " / " + totalPixels);
    }
}
