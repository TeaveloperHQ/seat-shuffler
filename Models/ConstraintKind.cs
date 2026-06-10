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
    ForbiddenPair = 5, // 가급적 멀리
    RequiredPair = 6,  // 가급적 가깝게
    FixedSeat = 7,     // 자리 고정
    AvoidSeat = 8,     // 자리 회피
}

public static class ConstraintKindInfo
{
    /// <summary>사용자가 순서를 정하는 '제약'(기본 우선순위, 앞=높음=나중에 완화).</summary>
    public static readonly IReadOnlyList<ConstraintKind> ConstraintKinds = new[]
    {
        ConstraintKind.FixedSeat,
        ConstraintKind.AvoidSeat,
        ConstraintKind.RequiredPair,
        ConstraintKind.ForbiddenPair,
        ConstraintKind.GenderSeat,
        ConstraintKind.FrontRow,
    };

    /// <summary>배정 '옵션' 3종 — 항상 제약보다 먼저 양보(가장 낮은 우선순위), 고정 순서.</summary>
    public static readonly IReadOnlyList<ConstraintKind> OptionKinds = new[]
    {
        ConstraintKind.SamePair,
        ConstraintKind.SameSeat,
        ConstraintKind.GenderPairing,
    };

    public static string ToKorean(this ConstraintKind k) => k switch
    {
        ConstraintKind.RequiredPair => "가급적 가깝게",
        ConstraintKind.ForbiddenPair => "가급적 멀리",
        ConstraintKind.GenderSeat => "남녀 자리",
        ConstraintKind.FrontRow => "앞자리",
        ConstraintKind.SamePair => "이전과 같은 짝 회피",
        ConstraintKind.SameSeat => "이전과 같은 자리 회피",
        ConstraintKind.GenderPairing => "동성/이성 짝",
        ConstraintKind.FixedSeat => "자리 고정",
        ConstraintKind.AvoidSeat => "자리 회피",
        _ => k.ToString(),
    };

    public static string Subtitle(this ConstraintKind k) => k switch
    {
        ConstraintKind.RequiredPair => "가까이/짝으로 앉히기 (가까이 ~ 반드시 짝)",
        ConstraintKind.ForbiddenPair => "떨어뜨려 앉히기 (같은 짝만 금지 ~ 팔방 금지 ~ 멀리)",
        ConstraintKind.GenderSeat => "좌석 설정 탭에서 남자리/여자리를 지정합니다",
        ConstraintKind.FrontRow => "앞쪽에 앉아야 하는 학생",
        ConstraintKind.FixedSeat => "특정 학생을 특정 좌석에 고정",
        ConstraintKind.AvoidSeat => "특정 학생이 특정 좌석을 피하게",
        _ => "",
    };
}
