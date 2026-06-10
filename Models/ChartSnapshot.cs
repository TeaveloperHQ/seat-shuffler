using System.Collections.Generic;

namespace SeatShuffler.Models;

/// <summary>출력용 좌석 1칸 스냅샷(배정 결과를 꾸미기/출력에 전달).</summary>
public sealed class ChartSeat
{
    public int Section { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public string Name { get; set; } = "";
    public string SubText { get; set; } = "";
    public Gender Gender { get; set; }
}

/// <summary>마지막 배정 결과의 출력용 스냅샷(비영속).</summary>
public sealed class ChartSnapshot
{
    public SeatGridConfig Config { get; set; } = new();
    public List<ChartSeat> Seats { get; set; } = new();
    public HashSet<SeatPosition> Empties { get; set; } = new();
    public Dictionary<SeatPosition, Gender> GenderSeats { get; set; } = new();
}
