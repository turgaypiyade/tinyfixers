using System;
using System.Security.Cryptography;
using System.Text;

public static class AuthNonce
{
    public static string Create()
    {
        var bytes = new byte[32];
        using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
    public static string Sha256(string value)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        var text = new StringBuilder(hash.Length * 2);
        foreach (byte b in hash) text.Append(b.ToString("x2"));
        return text.ToString();
    }
}
