using System;
using System.Security.Cryptography;
using System.Text;

class Test {
    static void Main() {
        byte[] salt = new byte[16];
        using (var rng = new RNGCryptoServiceProvider()) {
            rng.GetBytes(salt);
        }
        Console.WriteLine("Salt generated");
    }
}
