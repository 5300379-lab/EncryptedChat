using System;
using System.Security.Cryptography;
using System.Text;

namespace EncryptedChat
{
    /// <summary>
    /// AES-256-CBC encryption with PKCS7 padding.
    /// Wire-compatible with the Python server (server_vps.py): key = SHA-256(password),
    /// output = base64( IV[16] + AES-CBC-PKCS7(plaintext) ), UTF-8 plaintext with NO BOM.
    /// </summary>
    public class AESCipher
    {
        private readonly byte[] _key;

        // UTF-8 WITHOUT a byte-order-mark. Using Encoding.UTF8 here would emit a BOM
        // through StreamWriter, which gets encrypted into the payload and makes the
        // Python server's json.loads() reject every message. Must stay BOM-free.
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public AESCipher(string password)
        {
            // Generate key from password using SHA-256 (same as Python)
            using (var sha256 = SHA256.Create())
            {
                _key = sha256.ComputeHash(Utf8NoBom.GetBytes(password));
            }
        }

        public string Encrypt(string plaintext)
        {
            using (var aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.KeySize = 256;
                aes.Key = _key;
                aes.GenerateIV();

                byte[] plainBytes = Utf8NoBom.GetBytes(plaintext);
                using (var encryptor = aes.CreateEncryptor())
                {
                    byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                    // Output = IV (16 bytes) + ciphertext, Base64-encoded
                    byte[] result = new byte[aes.IV.Length + cipherBytes.Length];
                    Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
                    Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);
                    return Convert.ToBase64String(result);
                }
            }
        }

        public string? Decrypt(string encodedCiphertext)
        {
            try
            {
                byte[] data = Convert.FromBase64String(encodedCiphertext);

                // Need at least a full IV; anything shorter is not a valid message
                // (also avoids a negative-length allocation on stray/empty frames).
                if (data.Length < 16)
                    return null;

                byte[] iv = new byte[16];
                Array.Copy(data, 0, iv, 0, 16);

                byte[] ciphertext = new byte[data.Length - 16];
                Array.Copy(data, 16, ciphertext, 0, data.Length - 16);

                using (var aes = Aes.Create())
                {
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.KeySize = 256;
                    aes.Key = _key;
                    aes.IV = iv;

                    using (var decryptor = aes.CreateDecryptor())
                    {
                        byte[] plainBytes = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
                        return Utf8NoBom.GetString(plainBytes);
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
