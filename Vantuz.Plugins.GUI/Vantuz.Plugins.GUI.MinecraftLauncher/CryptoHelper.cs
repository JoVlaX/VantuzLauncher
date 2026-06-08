using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Vantuz.Plugins.GUI.MinecraftLauncher;

/// <summary>
/// Portable Cryptography СЃ РґРёРЅР°РјРёС‡РµСЃРєРѕР№ СЃР»СѓС‡Р°Р№РЅРѕР№ СЃРѕР»СЊСЋ.
/// Per INVARIANT_THEORY.md В§3.2 Nomadic Invariant: РёСЃРїРѕР»СЊР·СѓРµС‚ MachineName + UserName РєР°Рє РєР»СЋС‡.
/// РЎРѕР»СЊ РіРµРЅРµСЂРёСЂСѓРµС‚СЃСЏ РґРёРЅР°РјРёС‡РµСЃРєРё Рё С…СЂР°РЅРёС‚СЃСЏ РІ РѕС‚РєСЂС‹С‚РѕРј РІРёРґРµ РІ Р·Р°РіРѕР»РѕРІРєРµ Р·Р°С€РёС„СЂРѕРІР°РЅРЅС‹С… РґР°РЅРЅС‹С….
/// F_doc: {decryption produces corrupted output or throws CryptographicException}
/// E_doc: Unit test round-trip Encrypt then Decrypt with random plaintext
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
            // Р“РµРЅРµСЂРёСЂСѓРµРј СЃР»СѓС‡Р°Р№РЅСѓСЋ СЃРѕР»СЊ РґР»СЏ РєР°Р¶РґРѕРіРѕ С€РёС„СЂРѕРІР°РЅРёСЏ
            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            using Aes aes = Aes.Create();
            // РСЃРїРѕР»СЊР·СѓРµРј РґРёРЅР°РјРёС‡РµСЃРєСѓСЋ СЃРѕР»СЊ РІРјРµСЃС‚Рѕ С…Р°СЂРґРєРѕРґ
            using var rfc2898 = new Rfc2898DeriveBytes(
                Environment.MachineName + Environment.UserName, // РќРѕРјР°РґРЅС‹Р№ РїСЂРѕС„РёР»СЊ
                salt,
                Iterations,
                HashAlgorithmName.SHA256);
            aes.Key = rfc2898.GetBytes(KeySize);
            aes.IV = rfc2898.GetBytes(IvSize);

            using MemoryStream ms = new MemoryStream();
            // Р—Р°РїРёСЃС‹РІР°РµРј СЃРѕР»СЊ РІ РѕС‚РєСЂС‹С‚РѕРј РІРёРґРµ РІ Р·Р°РіРѕР»РѕРІРѕРє
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

            // РР·РІР»РµРєР°РµРј СЃРѕР»СЊ РёР· Р·Р°РіРѕР»РѕРІРєР°
            byte[] salt = new byte[SaltSize];
            Buffer.BlockCopy(cipherBytes, 0, salt, 0, SaltSize);
            byte[] encryptedData = new byte[cipherBytes.Length - SaltSize];
            Buffer.BlockCopy(cipherBytes, SaltSize, encryptedData, 0, encryptedData.Length);

            using Aes aes = Aes.Create();
            // РСЃРїРѕР»СЊР·СѓРµРј РёР·РІР»РµС‡РµРЅРЅСѓСЋ СЃРѕР»СЊ
            using var rfc2898 = new Rfc2898DeriveBytes(
                Environment.MachineName + Environment.UserName, // РќРѕРјР°РґРЅС‹Р№ РїСЂРѕС„РёР»СЊ
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
