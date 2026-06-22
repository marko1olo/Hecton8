using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

class Test {
    static void Main() {
        // Generating random salt
        byte[] salt = new byte[16];
        using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider()) {
            rng.GetBytes(salt);
        }
        Console.WriteLine(Convert.ToBase64String(salt));
    }
}
