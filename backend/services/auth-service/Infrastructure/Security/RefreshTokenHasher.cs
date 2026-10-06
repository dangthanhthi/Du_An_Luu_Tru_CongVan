using System.Security.Cryptography;
using System.Text;

namespace AuthService;

public static class RefreshTokenHasher
{
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
