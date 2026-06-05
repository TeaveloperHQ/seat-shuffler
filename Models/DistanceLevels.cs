namespace SeatShuffler.Models;

/// <summary>가급적 멀리/가깝게의 거리 단계와 라벨·거리 매핑.</summary>
public static class DistanceLevels
{
    // 가급적 멀리 (ForbiddenPairs)
    public const int ApartDeskmate = 1;  // 같은 짝만 금지(앞뒤·대각 허용)
    public const int ApartNeighbors = 2; // 팔방(옆·앞뒤·대각 8칸) 금지
    public const int ApartFar = 3;       // 멀리(2칸+)

    // 가급적 가깝게 (RequiredPairs)
    public const int CloseNear = 1;      // 가까이(팔방 안)
    public const int CloseDeskmate = 2;  // 반드시 짝

    public static int NormalizeApart(int lvl) => lvl <= 0 ? ApartDeskmate : lvl;
    public static int NormalizeClose(int lvl) => lvl <= 0 ? CloseDeskmate : lvl;

    public static string ApartLabel(int lvl) => NormalizeApart(lvl) switch
    {
        ApartNeighbors => "팔방 금지",
        ApartFar => "멀리",
        _ => "같은 짝만 금지",
    };

    public static string CloseLabel(int lvl) => NormalizeClose(lvl) switch
    {
        CloseNear => "가까이",
        _ => "반드시 짝",
    };

    /// <summary>멀리: 요구되는 최소 Chebyshev 거리. -1이면 '같은 짝만 금지'(짝 그룹 단위 처리).</summary>
    public static int ApartMinDistance(int lvl) => NormalizeApart(lvl) switch
    {
        ApartNeighbors => 2,
        ApartFar => 3,
        _ => -1,
    };

    /// <summary>가깝게: 허용되는 최대 Chebyshev 거리. -1이면 '반드시 짝'(짝 그룹 단위 처리).</summary>
    public static int CloseMaxDistance(int lvl) => NormalizeClose(lvl) switch
    {
        CloseNear => 1,
        _ => -1,
    };
}
