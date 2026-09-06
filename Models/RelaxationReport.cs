using System.Collections.Generic;

namespace SeatShuffler.Models;

/// <summary>배정 시 어떤 제약이 자동 완화되었는지 보고.</summary>
public sealed class RelaxationReport
{
    public bool RelaxedGenderPairing { get; set; }

    /// <summary>동성/이성 짝을 지키되 어쩔 수 없이 예외로 둔 짝 수(0이면 완전히 지켜짐).</summary>
    public int GenderPairExceptions { get; set; }

    public bool RelaxedSameSeat { get; set; }
    public bool RelaxedSamePair { get; set; }
    public bool RelaxedFrontRow { get; set; }
    public bool RelaxedGenderSeat { get; set; }
    public bool RelaxedForbiddenPair { get; set; }
    public bool RelaxedRequiredPair { get; set; }
    public bool RelaxedFixedSeat { get; set; }
    public bool RelaxedAvoidSeat { get; set; }

    public bool AnyRelaxed =>
        RelaxedGenderPairing || GenderPairExceptions > 0 || RelaxedSameSeat || RelaxedSamePair ||
        RelaxedFrontRow || RelaxedGenderSeat || RelaxedForbiddenPair || RelaxedRequiredPair ||
        RelaxedFixedSeat || RelaxedAvoidSeat;

    /// <summary>완화 사다리 순서(성별 → 같은자리 → 같은짝 → 앞자리 → 짝금지 → 짝필수)대로 안내.</summary>
    public string Summary
    {
        get
        {
            if (!AnyRelaxed) return "";

            // 성별 짝 예외만 있는 경우는 '최대한 맞췄다'는 뜻이라 따로 안내한다.
            bool onlyGenderExceptions = GenderPairExceptions > 0 && !RelaxedGenderPairing &&
                !RelaxedSameSeat && !RelaxedSamePair && !RelaxedFrontRow && !RelaxedGenderSeat &&
                !RelaxedForbiddenPair && !RelaxedRequiredPair && !RelaxedFixedSeat && !RelaxedAvoidSeat;
            if (onlyGenderExceptions)
                return $"성별 짝을 최대한 맞췄지만 {GenderPairExceptions}쌍은 예외입니다(인원 구성상 불가피).";

            var parts = new List<string>();
            if (RelaxedGenderPairing) parts.Add("동성/이성 짝");
            else if (GenderPairExceptions > 0) parts.Add($"동성/이성 짝(예외 {GenderPairExceptions}쌍)");
            if (RelaxedSameSeat) parts.Add("같은 자리 회피");
            if (RelaxedSamePair) parts.Add("같은 짝 회피");
            if (RelaxedFrontRow) parts.Add("앞자리 지정");
            if (RelaxedGenderSeat) parts.Add("남녀 자리 지정");
            if (RelaxedForbiddenPair) parts.Add("가급적 멀리");
            if (RelaxedRequiredPair) parts.Add("가급적 가깝게");
            if (RelaxedFixedSeat) parts.Add("자리 고정");
            if (RelaxedAvoidSeat) parts.Add("자리 회피");
            return $"제약 완화: {string.Join(", ", parts)} 조건을 모두 만족할 수 없어 완화했습니다.";
        }
    }
}
