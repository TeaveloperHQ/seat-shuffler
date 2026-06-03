namespace SeatShuffler.Models;

/// <summary>학생 성별. 미지정은 가져오기 시 인식 실패한 경우.</summary>
public enum Gender
{
    Unspecified = 0,
    Male = 1,   // 남
    Female = 2, // 녀
}

public static class GenderExtensions
{
    public static string ToKorean(this Gender g) => g switch
    {
        Gender.Male => "남",
        Gender.Female => "녀",
        _ => "미지정",
    };
}
