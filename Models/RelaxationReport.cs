using System.Collections.Generic;

namespace SeatShuffler.Models;

/// <summary>배정 시 어떤 제약이 자동 완화되었는지 보고.</summary>
public sealed class RelaxationReport
{
    public bool RelaxedGenderPairing { get; set; }

    /// <summary>제약을 지키되 어쩔 수 없이 예외로 둔 건수(0이면 완전히 지켜짐).</summary>
    public int GenderPairExceptions { get; set; }   // 동성/이성 짝 — 예외 짝 수
    public int SameSeatExceptions { get; set; }     // 같은 자리 회피 — 예외 학생 수
    public int SamePairExceptions { get; set; }     // 같은 짝 회피 — 예외 짝 수

    private bool AnyExceptions => GenderPairExceptions > 0 || SameSeatExceptions > 0 || SamePairExceptions > 0;

    public bool RelaxedSameSeat { get; set; }
    public bool RelaxedSamePair { get; set; }
    public bool RelaxedFrontRow { get; set; }
    public bool RelaxedGenderSeat { get; set; }
    public bool RelaxedForbiddenPair { get; set; }
    public bool RelaxedRequiredPair { get; set; }
    public bool RelaxedFixedSeat { get; set; }
    public bool RelaxedAvoidSeat { get; set; }

    public bool AnyRelaxed =>
        RelaxedGenderPairing || AnyExceptions || RelaxedSameSeat || RelaxedSamePair ||
        RelaxedFrontRow || RelaxedGenderSeat || RelaxedForbiddenPair || RelaxedRequiredPair ||
        RelaxedFixedSeat || RelaxedAvoidSeat;

    /// <summary>완화 사다리 순서(성별 → 같은자리 → 같은짝 → 앞자리 → 짝금지 → 짝필수)대로 안내.</summary>
    public string Summary
    {
        get
        {
            if (!AnyRelaxed) return "";

            // ① 아예 포기한 제약
            var parts = new List<string>();
            if (RelaxedGenderPairing) parts.Add("동성/이성 짝");
            if (RelaxedSameSeat) parts.Add("같은 자리 회피");
            if (RelaxedSamePair) parts.Add("같은 짝 회피");
            if (RelaxedFrontRow) parts.Add("앞자리 지정");
            if (RelaxedGenderSeat) parts.Add("남녀 자리 지정");
            if (RelaxedForbiddenPair) parts.Add("가급적 멀리");
            if (RelaxedRequiredPair) parts.Add("가급적 가깝게");
            if (RelaxedFixedSeat) parts.Add("자리 고정");
            if (RelaxedAvoidSeat) parts.Add("자리 회피");

            // ② 최대한 지키고 어쩔 수 없는 만큼만 예외로 둔 제약
            var exceptions = new List<string>();
            if (GenderPairExceptions > 0) exceptions.Add($"성별 짝 {GenderPairExceptions}쌍");
            if (SameSeatExceptions > 0) exceptions.Add($"같은 자리 {SameSeatExceptions}명");
            if (SamePairExceptions > 0) exceptions.Add($"같은 짝 {SamePairExceptions}쌍");

            var except = exceptions.Count > 0
                ? $"최대한 지켰지만 다음은 구성상 불가피한 예외입니다 — {string.Join(", ", exceptions)}."
                : "";
            if (parts.Count == 0) return except;

            var relaxed = $"제약 완화: {string.Join(", ", parts)} 조건을 모두 만족할 수 없어 완화했습니다.";
            return except.Length > 0 ? $"{relaxed} {except}" : relaxed;
        }
    }
}
