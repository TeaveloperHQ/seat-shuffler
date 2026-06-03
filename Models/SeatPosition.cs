namespace SeatShuffler.Models;

/// <summary>물리적 좌석 한 칸. (분단, 행, 열) 좌표.</summary>
public readonly record struct SeatPosition(int Section, int Row, int Col)
{
    /// <summary>기록 비교용 좌석 키. 그리드 좌표가 같아야 "같은 자리"로 본다.</summary>
    public string Key => $"{Section}:{Row}:{Col}";

    public override string ToString() => Key;
}
