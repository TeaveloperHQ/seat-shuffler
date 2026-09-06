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

    /// <summary>출력 스킨 ID.</summary>
    public string SkinId { get; set; } = "basic";

    /// <summary>교탁 기준 출력(좌석 거울반전).</summary>
    public bool FlipForTeacher { get; set; }

    /// <summary>커스텀 배경 이미지 경로(없으면 스킨 배경색 사용).</summary>
    public string? BackgroundImagePath { get; set; }

    /// <summary>남자리/여자리 셀 이미지 경로(없으면 스킨 카드색 사용).</summary>
    public string? MaleCellImagePath { get; set; }
    public string? FemaleCellImagePath { get; set; }

    /// <summary>셀 이미지를 투명 배경으로(테두리·둥근클립 없이 이미지 모양 그대로).</summary>
    public bool TransparentCells { get; set; }

    /// <summary>좌석을 남/여 색으로 구분. 끄면 모든 칸이 한 가지 색이고 셀 이미지도 쓰지 않는다.</summary>
    public bool GenderColors { get; set; } = true;

    /// <summary>출력에 '칠판(앞)' 표시(배경과 안 맞으면 끌 수 있음).</summary>
    public bool ShowBoard { get; set; } = true;
}
