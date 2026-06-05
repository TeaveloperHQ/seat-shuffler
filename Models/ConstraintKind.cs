using System.Collections.Generic;

namespace SeatShuffler.Models;

/// <summary>완화 사다리에서 우선순위를 매길 수 있는 제약 종류.</summary>
public enum ConstraintKind
{
    GenderPairing = 0, // 동성/이성 짝
    SameSeat = 1,      // 이전과 같은 자리 회피
    SamePair = 2,      // 이전과 같은 짝 회피
    FrontRow = 3,      // 앞자리
    GenderSeat = 4,    // 남녀 자리
    ForbiddenPair = 5, // 짝 금지
    RequiredPair = 6,  // 짝 필수
}

public static class ConstraintKindInfo
{
    /// <summary>기본 우선순위(앞=높음=나중에 완화). 사회적 제약일수록 높게.</summary>
    public static readonly IReadOnlyList<ConstraintKind> DefaultPriority = new[]
    {
        ConstraintKind.RequiredPair,
        ConstraintKind.ForbiddenPair,
        ConstraintKind.GenderSeat,
        ConstraintKind.FrontRow,
        ConstraintKind.SamePair,
        ConstraintKind.SameSeat,
        ConstraintKind.GenderPairing,
    };

    public static string ToKorean(this ConstraintKind k) => k switch
    {
        ConstraintKind.RequiredPair => "짝 필수",
        ConstraintKind.ForbiddenPair => "짝 금지",
        ConstraintKind.GenderSeat => "남녀 자리",
        ConstraintKind.FrontRow => "앞자리",
        ConstraintKind.SamePair => "이전과 같은 짝 회피",
        ConstraintKind.SameSeat => "이전과 같은 자리 회피",
        ConstraintKind.GenderPairing => "동성/이성 짝",
        _ => k.ToString(),
    };
}
