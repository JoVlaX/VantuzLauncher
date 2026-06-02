using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Vantuz.Products.MinecraftLauncher.GUI;

/// <summary>
/// Portable Cryptography с динамической случайной солью.
/// Per INVARIANT_THEORY.md §3.2 Nomadic Invariant: использует MachineName + UserName как ключ.
/// Соль генерируется динамически и хранится в открытом виде в заголовке зашифрованных данных.
/// </summary>
public static class CryptoHelper
{
    private const int SaltSize = 16; // 128 bits
    private const int KeySize = 32;  // 256 bits
    private const int IvSize = 16;   // 128 bits
    private const int Iterations = 10000;

    public static string Encrypt(string clearText)
    {
        if (string.IsNullOrEmpty(clearText)) return clearText;
        try
        {
            // Генерируем случайную соль для каждого шифрования
            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            using Aes aes = Aes.Create();
            // Используем динамическую соль вместо хардкод
            using var rfc2898 = new Rfc2898DeriveBytes(
                Environment.MachineName + Environment.UserName, // Номадный профиль
                salt,
                Iterations,
                HashAlgorithmName.SHA256);
            aes.Key = rfc2898.GetBytes(KeySize);
            aes.IV = rfc2898.GetBytes(IvSize);

            using MemoryStream ms = new MemoryStream();
            // Записываем соль в открытом виде в заголовок
            ms.Write(salt, 0, salt.Length);
            using CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(Encoding.UTF8.GetBytes(clearText));
            cs.Close();
            return Convert.ToBase64String(ms.ToArray());
        }
        catch { return ""; }
    }

    public static string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;
        try
        {
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            if (cipherBytes.Length < SaltSize) return "";

            // Извлекаем соль из заголовка
            byte[] salt = new byte[SaltSize];
            Buffer.BlockCopy(cipherBytes, 0, salt, 0, SaltSize);
            byte[] encryptedData = new byte[cipherBytes.Length - SaltSize];
            Buffer.BlockCopy(cipherBytes, SaltSize, encryptedData, 0, encryptedData.Length);

            using Aes aes = Aes.Create();
            // Используем извлеченную соль
            using var rfc2898 = new Rfc2898DeriveBytes(
                Environment.MachineName + Environment.UserName, // Номадный профиль
                salt,
                Iterations,
                HashAlgorithmName.SHA256);
            aes.Key = rfc2898.GetBytes(KeySize);
            aes.IV = rfc2898.GetBytes(IvSize);

            using MemoryStream ms = new MemoryStream();
            using CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write);
            cs.Write(encryptedData);
            cs.Close();
            return Encoding.UTF8.GetString(ms.ToArray());
        }
        catch { return ""; }
    }
}
