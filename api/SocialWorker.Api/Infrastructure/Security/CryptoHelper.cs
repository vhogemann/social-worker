using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SocialWorker.Api.Infrastructure.Security;

public static class CryptoHelper
{
    public static string EncryptString(string plainText, string base64Key)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        byte[] key = Convert.FromBase64String(base64Key);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        
        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length); // prepend IV
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
        {
            sw.Write(plainText);
        }
        
        return Convert.ToBase64String(ms.ToArray());
    }

    public static string DecryptString(string cipherText, string base64Key)
    {
        if (string.IsNullOrEmpty(cipherText)) return "";
        byte[] fullCipher = Convert.FromBase64String(cipherText);
        byte[] key = Convert.FromBase64String(base64Key);
        
        using var aes = Aes.Create();
        aes.Key = key;
        byte[] iv = new byte[aes.BlockSize / 8];
        byte[] cipher = new byte[fullCipher.Length - iv.Length];
        
        Array.Copy(fullCipher, iv, iv.Length);
        Array.Copy(fullCipher, iv.Length, cipher, 0, cipher.Length);
        aes.IV = iv;
        
        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream(cipher);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        return sr.ReadToEnd();
    }
}
