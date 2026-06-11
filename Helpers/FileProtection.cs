using System.Security.Cryptography;
using System.Text;

namespace AccountManagement.Helpers;

/// <summary>
/// 对 App_Data 文件进行 AES 加密保护，防止非授权查看。
/// </summary>
public static class FileProtection
{
    // 固定密钥和IV（生产环境可改为从配置文件读取）
    private static readonly byte[] s_key = SHA256.HashData(Encoding.UTF8.GetBytes("GARCHINA@2026#AccountMgmt_SecureKey!"));
    private static readonly byte[] s_iv = MD5.HashData(Encoding.UTF8.GetBytes("CN_IT_Support@sinarmas-agri.com"));

    public static string ReadAllText(string path)
    {
        if (!System.IO.File.Exists(path)) return "";
        var encrypted = System.IO.File.ReadAllBytes(path);
        var decrypted = Decrypt(encrypted);
        return Encoding.UTF8.GetString(decrypted);
    }

    public static void WriteAllText(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir != null) Directory.CreateDirectory(dir);
        var encrypted = Encrypt(Encoding.UTF8.GetBytes(content));
        System.IO.File.WriteAllBytes(path, encrypted);
    }

    public static string[] ReadAllLines(string path)
    {
        var text = ReadAllText(path);
        return string.IsNullOrEmpty(text) ? Array.Empty<string>() : text.Split('\n');
    }

    public static void AppendAllText(string path, string content)
    {
        var existing = ReadAllText(path);
        WriteAllText(path, existing + content);
    }

    public static void Delete(string path)
    {
        if (System.IO.File.Exists(path))
            System.IO.File.Delete(path);
    }

    public static bool Exists(string path)
    {
        return System.IO.File.Exists(path);
    }

    private static byte[] Encrypt(byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = s_key;
        aes.IV = s_iv;
        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
        cs.Write(data, 0, data.Length);
        cs.FlushFinalBlock();
        return ms.ToArray();
    }

    private static byte[] Decrypt(byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = s_key;
        aes.IV = s_iv;
        using var ms = new MemoryStream(data);
        using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var result = new MemoryStream();
        cs.CopyTo(result);
        return result.ToArray();
    }
}
