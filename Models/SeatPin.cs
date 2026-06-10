namespace SeatShuffler.Models;

/// <summary>학생을 특정 좌석에 고정/회피하는 제약(영속). 학생은 식별키로 참조.</summary>
public sealed class SeatPin
{
    public string StudentKey { get; set; } = "";
    public int Section { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }

    public SeatPosition Position => new(Section, Row, Col);

    /// <summary>같은 학생+좌석 중복 판별 키.</summary>
    public string Key => $"{StudentKey}@{Section}:{Row}:{Col}";
}
