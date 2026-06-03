using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SeatShuffler.Models;

/// <summary>좌석 1칸의 확정 배치(영속용 평면 구조).</summary>
public sealed class PlacementDto
{
    public string StudentKey { get; set; } = "";
    public string StudentName { get; set; } = ""; // 표시용 스냅샷
    public Gender Gender { get; set; } = Gender.Unspecified;
    public int Section { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }

    [JsonIgnore]
    public SeatPosition Position => new(Section, Row, Col);
}

/// <summary>확정되어 기록 탭에 누적되는 배치 1건. 다음 배정의 회피 기준.</summary>
public sealed class ConfirmedRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset ConfirmedAt { get; set; }
    public string Label { get; set; } = "";
    public SeatGridConfig Config { get; set; } = new();
    public List<PlacementDto> Placements { get; set; } = new();

    /// <summary>구성 요약(예: "분단3 · 행4 · 열2").</summary>
    [JsonIgnore]
    public string ConfigSummary =>
        $"분단{Config.Sections} · 행{Config.Rows} · 열{Config.Cols}";

    [JsonIgnore]
    public int StudentCount => Placements.Count;
}
