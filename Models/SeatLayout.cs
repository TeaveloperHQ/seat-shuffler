using System.Collections.Generic;

namespace SeatShuffler.Models;

/// <summary>짝 그룹: 좌석 1~2칸. B가 null이면 단독석(IsSingle).</summary>
public sealed class PairGroup
{
    public required SeatPosition A { get; init; }
    public SeatPosition? B { get; init; }
    public bool IsSingle => B is null;
}

/// <summary>
/// <see cref="SeatGridConfig"/>에서 계산되는 좌석 구조(비영속).
/// 좌석 순서와 짝 그룹(같은 행 인접 2칸씩, 홀수 잔여는 단독석)을 담는다.
/// </summary>
public sealed class SeatLayout
{
    public required SeatGridConfig Config { get; init; }
    public required IReadOnlyList<SeatPosition> Positions { get; init; }
    public required IReadOnlyList<PairGroup> Pairs { get; init; }

    public int SeatCount => Positions.Count;

    public static SeatLayout Build(SeatGridConfig config, IReadOnlySet<SeatPosition>? empties = null)
    {
        bool IsEmpty(SeatPosition p) => empties is not null && empties.Contains(p);

        var positions = new List<SeatPosition>();
        var pairs = new List<PairGroup>();

        for (int s = 0; s < config.Sections; s++)
        {
            for (int r = 0; r < config.Rows; r++)
            {
                // 한 행을 좌→우로 훑으며 2칸씩 묶는다. 홀수 잔여 또는 짝 한쪽이 빈자리면 단독석.
                for (int c = 0; c < config.Cols; c += 2)
                {
                    var a = new SeatPosition(s, r, c);
                    SeatPosition? b = (c + 1 < config.Cols) ? new SeatPosition(s, r, c + 1) : null;

                    bool aEmpty = IsEmpty(a);
                    bool bEmpty = b is not null && IsEmpty(b.Value);

                    if (!aEmpty) positions.Add(a);
                    if (b is not null && !bEmpty) positions.Add(b.Value);

                    if (!aEmpty && b is not null && !bEmpty)
                        pairs.Add(new PairGroup { A = a, B = b });          // 정상 짝
                    else if (!aEmpty && (b is null || bEmpty))
                        pairs.Add(new PairGroup { A = a, B = null });        // 단독석(왼쪽만)
                    else if (aEmpty && b is not null && !bEmpty)
                        pairs.Add(new PairGroup { A = b.Value, B = null });  // 단독석(오른쪽만)
                    // 둘 다 비면 그룹 없음
                }
            }
        }

        return new SeatLayout { Config = config, Positions = positions, Pairs = pairs };
    }
}
