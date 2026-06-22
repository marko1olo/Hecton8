using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

class Test {
    static void Main() {
        string textToEncrypt = "Hello world!";
        string key = "mysecretkey";

        byte[] encryptedBytes;

        // Encrypt
        using (MemoryStream inputStream = new MemoryStream(Encoding.UTF8.GetBytes(textToEncrypt)))
        using (MemoryStream outputStream = new MemoryStream()) {
            byte[] salt = new byte[16];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider()) {
                rng.GetBytes(salt);
            }
            outputStream.Write(salt, 0, salt.Length);

            RijndaelManaged algorithm = new RijndaelManaged();
            Rfc2898DeriveBytes rfcKey = new Rfc2898DeriveBytes(key, salt);

            algorithm.Key = rfcKey.GetBytes(algorithm.KeySize / 8);
            algorithm.IV = rfcKey.GetBytes(algorithm.BlockSize / 8);

            using (CryptoStream cryptostream = new CryptoStream(inputStream, algorithm.CreateEncryptor(), CryptoStreamMode.Read)) {
                cryptostream.CopyTo(outputStream);
            }
            encryptedBytes = outputStream.ToArray();
        }

        // Decrypt
        using (MemoryStream inputStream = new MemoryStream(encryptedBytes))
        using (MemoryStream outputStream = new MemoryStream()) {
            byte[] salt = new byte[16];
            inputStream.Read(salt, 0, salt.Length);

            RijndaelManaged algorithm = new RijndaelManaged();
            Rfc2898DeriveBytes rfcKey = new Rfc2898DeriveBytes(key, salt);

            algorithm.Key = rfcKey.GetBytes(algorithm.KeySize / 8);
            algorithm.IV = rfcKey.GetBytes(algorithm.BlockSize / 8);

            using (CryptoStream cryptostream = new CryptoStream(inputStream, algorithm.CreateDecryptor(), CryptoStreamMode.Read)) {
                cryptostream.CopyTo(outputStream);
            }
            Console.WriteLine("Decrypted: " + Encoding.UTF8.GetString(outputStream.ToArray()));
        }
    }
}
