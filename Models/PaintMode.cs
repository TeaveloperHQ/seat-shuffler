namespace SeatShuffler.Models;

/// <summary>좌석 설정 탭의 칠하기 모드.</summary>
public enum PaintMode
{
    Free = 0,   // 자유(지정 해제)
    Empty = 1,  // 빈자리(배정 제외)
    Male = 2,   // 남자리
    Female = 3, // 여자리
}
