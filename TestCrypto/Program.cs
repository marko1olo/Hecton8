using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

// Hardened 2026-07-26 by security audit.
//   Was: RijndaelManaged + PBKDF2 defaults (1000 iterations, SHA-1) + unauthenticated CBC.
//   Now: AES-256-GCM (authenticated encryption) + PBKDF2-HMAC-SHA256 @ 600k iterations.
// This is only a scratch demo. Preferred action is to remove it from the repo:
//   git rm -r --cached TestCrypto  &&  rmdir /s /q TestCrypto
internal static class Test
{
    private const int SaltSize = 16;   // 128-bit KDF salt
    private const int NonceSize = 12;  // 96-bit GCM nonce
    private const int TagSize = 16;    // 128-bit GCM auth tag
    private const int KdfIterations = 600_000;
    private const int KeySize = 32;    // 256-bit AES key

    private static void Main()
    {
        const string plaintext = "Hello world!";
        string password = Environment.GetEnvironmentVariable("ENCRYPTION_KEY")
            ?? throw new InvalidOperationException("ENCRYPTION_KEY environment variable is not set.");

        byte[] blob = Encrypt(plaintext, password);
        Console.WriteLine("Decrypted: " + Decrypt(blob, password));
    }

    // Output layout: salt | nonce | tag | ciphertext
    private static byte[] Encrypt(string plaintext, string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] key = DeriveKey(password, salt);
        try
        {
            byte[] pt = Encoding.UTF8.GetBytes(plaintext);
            byte[] ct = new byte[pt.Length];
            byte[] tag = new byte[TagSize];
            using (var gcm = new AesGcm(key, TagSize))
                gcm.Encrypt(nonce, pt, ct, tag);

            using var ms = new MemoryStream(SaltSize + NonceSize + TagSize + ct.Length);
            ms.Write(salt);
            ms.Write(nonce);
            ms.Write(tag);
            ms.Write(ct);
            return ms.ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static string Decrypt(byte[] blob, string password)
    {
        var salt = blob.AsSpan(0, SaltSize).ToArray();
        var nonce = blob.AsSpan(SaltSize, NonceSize).ToArray();
        var tag = blob.AsSpan(SaltSize + NonceSize, TagSize).ToArray();
        var ct = blob.AsSpan(SaltSize + NonceSize + TagSize).ToArray();
        byte[] key = DeriveKey(password, salt);
        try
        {
            byte[] pt = new byte[ct.Length];
            using (var gcm = new AesGcm(key, TagSize))
                gcm.Decrypt(nonce, ct, tag, pt); // throws CryptographicException on tampering
            return Encoding.UTF8.GetString(pt);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] DeriveKey(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, KdfIterations, HashAlgorithmName.SHA256, KeySize);
}
