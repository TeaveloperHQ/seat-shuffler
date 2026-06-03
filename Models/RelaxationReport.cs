namespace SeatShuffler.Models;

/// <summary>배정 시 어떤 제약이 자동 완화되었는지 보고.</summary>
public sealed class RelaxationReport
{
    public bool RelaxedSameSeat { get; set; }
    public bool RelaxedSamePair { get; set; }
    public bool RelaxedGenderPairing { get; set; }

    public bool AnyRelaxed => RelaxedSameSeat || RelaxedSamePair || RelaxedGenderPairing;

    /// <summary>사다리 순서(자리 → 짝 → 성별)대로 안내 문구 구성.</summary>
    public string Summary
    {
        get
        {
            if (!AnyRelaxed) return "";
            var parts = new System.Collections.Generic.List<string>();
            if (RelaxedSameSeat) parts.Add("같은 자리 회피");
            if (RelaxedSamePair) parts.Add("같은 짝 회피");
            if (RelaxedGenderPairing) parts.Add("성별 짝 조건");
            return $"제약 완화: {string.Join(" → ", parts)}을(를) 모두 만족할 수 없어 일부를 완화했습니다.";
        }
    }
}
