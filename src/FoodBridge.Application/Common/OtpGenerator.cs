using System.Security.Cryptography;
using System.Text;

namespace FoodBridge.Application.Common;

public static class OtpGenerator
{
    private const int CodeLength = 6;

    public static string GenerateCode()
    {
        Span<byte> buffer = stackalloc byte[4];
        RandomNumberGenerator.Fill(buffer);
        var value = BitConverter.ToUInt32(buffer) % 1_000_000u;
        return value.ToString(new string('0', CodeLength));
    }

    public static string Hash(string mobile, string code)
    {
        var bytes = Encoding.UTF8.GetBytes($"{mobile}:{code}");
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    public static bool Verify(string mobile, string code, string expectedHash)
    {
        var actualHash = Hash(mobile, code);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(actualHash),
            Encoding.UTF8.GetBytes(expectedHash));
    }
}
