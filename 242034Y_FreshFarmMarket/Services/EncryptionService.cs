using System.Security.Cryptography;
using System.Text;

namespace _242034Y_FreshFarmMarket.Services
{
    public interface IEncryptionService
    {
        string Encrypt(string plainText);
        string Decrypt(string cipherText);
    }

    public class EncryptionService : IEncryptionService
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;

        public EncryptionService(IConfiguration configuration)
        {
            // Get or generate encryption key
            var encryptionKey = configuration["Encryption:Key"];
            var encryptionIV = configuration["Encryption:IV"];

            if (string.IsNullOrEmpty(encryptionKey) || string.IsNullOrEmpty(encryptionIV))
            {
                // Generate new keys if not configured
                using var aes = Aes.Create();
                aes.GenerateKey();
                aes.GenerateIV();

                _key = aes.Key;
                _iv = aes.IV;
            }
            else
            {
                // Use configured keys
                _key = Encoding.UTF8.GetBytes(encryptionKey.PadRight(32).Substring(0, 32));
                _iv = Encoding.UTF8.GetBytes(encryptionIV.PadRight(16).Substring(0, 16));
            }
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            try
            {
                using var aes = Aes.Create();
                aes.Key = _key;
                aes.IV = _iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream();
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs))
                {
                    sw.Write(plainText);
                }

                return Convert.ToBase64String(ms.ToArray());
            }
            catch (Exception)
            {
                // In production, log this error
                return plainText;
            }
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            try
            {
                using var aes = Aes.Create();
                aes.Key = _key;
                aes.IV = _iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                var buffer = Convert.FromBase64String(cipherText);

                using var ms = new MemoryStream(buffer);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);
                return sr.ReadToEnd();
            }
            catch
            {
                return cipherText;
            }
        }
    }
}