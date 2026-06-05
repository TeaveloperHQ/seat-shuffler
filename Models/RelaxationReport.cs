using System.Collections.Generic;

namespace SeatShuffler.Models;

/// <summary>배정 시 어떤 제약이 자동 완화되었는지 보고.</summary>
public sealed class RelaxationReport
{
    public bool RelaxedGenderPairing { get; set; }
    public bool RelaxedSameSeat { get; set; }
    public bool RelaxedSamePair { get; set; }
    public bool RelaxedFrontRow { get; set; }
    public bool RelaxedGenderSeat { get; set; }
    public bool RelaxedForbiddenPair { get; set; }
    public bool RelaxedRequiredPair { get; set; }

    public bool AnyRelaxed =>
        RelaxedGenderPairing || RelaxedSameSeat || RelaxedSamePair ||
        RelaxedFrontRow || RelaxedGenderSeat || RelaxedForbiddenPair || RelaxedRequiredPair;

    /// <summary>완화 사다리 순서(성별 → 같은자리 → 같은짝 → 앞자리 → 짝금지 → 짝필수)대로 안내.</summary>
    public string Summary
    {
        get
        {
            if (!AnyRelaxed) return "";
            var parts = new List<string>();
            if (RelaxedGenderPairing) parts.Add("성별 짝");
            if (RelaxedSameSeat) parts.Add("같은 자리 회피");
            if (RelaxedSamePair) parts.Add("같은 짝 회피");
            if (RelaxedFrontRow) parts.Add("앞자리 지정");
            if (RelaxedGenderSeat) parts.Add("남녀 자리 지정");
            if (RelaxedForbiddenPair) parts.Add("짝 금지");
            if (RelaxedRequiredPair) parts.Add("짝 필수");
            return $"제약 완화: {string.Join(", ", parts)} 조건을 모두 만족할 수 없어 완화했습니다.";
        }
    }
}
