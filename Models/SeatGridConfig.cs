using System.Text.Json.Serialization;

namespace SeatShuffler.Models;

/// <summary>자리 배치 그리드 구성. 총 좌석 수 = Sections × Rows × Cols.</summary>
public sealed class SeatGridConfig
{
    public int Sections { get; init; } = 3; // 분단 수
    public int Rows { get; init; } = 4;     // 행 수(분단당)
    public int Cols { get; init; } = 2;     // 열 수(분단당)
    public PairMode PairMode { get; init; } = PairMode.SameGender;

    [JsonIgnore]
    public int TotalSeats => Sections * Rows * Cols;

    /// <summary>열 수가 2 이상이면 짝(인접 2칸)이 생긴다.</summary>
    [JsonIgnore]
    public bool HasPairs => Cols >= 2;

    public SeatGridConfig Clone() => new()
    {
        Sections = Sections,
        Rows = Rows,
        Cols = Cols,
        PairMode = PairMode,
    };
}
