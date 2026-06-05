using System.Collections.Generic;

namespace SeatShuffler.Models;

/// <summary>좌석 1칸 좌표(영속용 평면 DTO).</summary>
public sealed class SeatPosDto
{
    public int Section { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public SeatPosition ToPosition() => new(Section, Row, Col);
}

/// <summary>특정 좌석을 남자리/여자리로 지정(영속용 평면 DTO).</summary>
public sealed class GenderSeatDto
{
    public int Section { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public Gender Gender { get; set; }
    public SeatPosition ToPosition() => new(Section, Row, Col);
}

/// <summary>배정 탭 설정(영속). 마지막 그리드 구성·옵션·빈자리를 복원한다.</summary>
public sealed class AssignmentSettings
{
    public int Sections { get; set; } = 3;
    public int Rows { get; set; } = 4;
    public int Cols { get; set; } = 2;
    public PairMode PairMode { get; set; } = PairMode.SameGender;
    public bool AvoidSameSeat { get; set; } = true;
    public bool AvoidSamePair { get; set; } = true;

    /// <summary>비워둘(배정 제외) 좌석 좌표.</summary>
    public List<SeatPosDto> EmptySeats { get; set; } = new();

    /// <summary>남자리/여자리로 지정한 좌석.</summary>
    public List<GenderSeatDto> GenderSeats { get; set; } = new();
}
