using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SeatShuffler.Models;

/// <summary>
/// 학생 1명. DataGrid 인라인 편집을 위해 ObservableObject.
/// 기록 매칭의 식별키는 <see cref="Key"/>(학번, 없으면 이름).
/// </summary>
public partial class Student : ObservableObject
{
    [ObservableProperty] private string _studentNumber = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private Gender _gender = Gender.Unspecified;

    /// <summary>기록 비교용 안정 식별키. 학번이 있으면 학번, 없으면 이름.</summary>
    [JsonIgnore]
    public string Key =>
        string.IsNullOrWhiteSpace(StudentNumber) ? Name.Trim() : StudentNumber.Trim();

    [JsonIgnore]
    public bool HasName => !string.IsNullOrWhiteSpace(Name);
}
