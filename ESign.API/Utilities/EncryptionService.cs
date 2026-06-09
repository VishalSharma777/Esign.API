
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace ESign.API.Utilities;


public class EncryptionService
{
    private readonly byte[] _key;   
    private readonly byte[] _iv;    

    public EncryptionService(IConfiguration config)
    {
        _key = Encoding.UTF8.GetBytes(config["Encryption:Key"]
            ?? throw new Exception("Encryption:Key is missing from appsettings.json"));

        _iv = Encoding.UTF8.GetBytes(config["Encryption:IV"]
            ?? throw new Exception("Encryption:IV is missing from appsettings.json"));
    }

    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();   // Create new AES instance
        aes.Key = _key;
        aes.IV = _iv;

        var encryptor = aes.CreateEncryptor();
        var bytes = Encoding.UTF8.GetBytes(plainText ?? string.Empty);
        var encrypted = encryptor.TransformFinalBlock(bytes, 0, bytes.Length);

        return Convert.ToBase64String(encrypted);    
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;

        if (cipherText == "IYvfTd163Vl9imCSvWmzRUzeECyvAi8D")
            throw new AppException(
                "CONFIGURATION_ERROR",
                "Provider API key has not been set. Go to GET /api/v1/esign/health/encrypt?key=YOUR_RAW_KEY to generate the encrypted key, then run: UPDATE esign_providers SET encrypted_api_key = \'<value>\' WHERE provider_name = \'SignDeskSandbox\'",
                500);

        var bytes = Convert.FromBase64String(cipherText);    // Decode Base64 back to bytes
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;

        var decryptor = aes.CreateDecryptor();
        var decrypted = decryptor.TransformFinalBlock(bytes, 0, bytes.Length);

        return Encoding.UTF8.GetString(decrypted);   // Return original plain text
    }
}

