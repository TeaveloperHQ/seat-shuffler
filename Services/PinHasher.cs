using System;
using System.Security.Cryptography;
using System.Text;

namespace SeatShuffler.Services;

/// <summary>제약 탭 PIN 해싱. 위협 모델은 어깨너머 노출 방지라 SHA-256으로 충분.</summary>
public static class PinHasher
{
    public static string Hash(string pin)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("seatshuffler-pin:" + pin));
        return Convert.ToHexString(bytes);
    }

    public static bool Verify(string pin, string? hash)
        => !string.IsNullOrEmpty(hash) && Hash(pin) == hash;
}
