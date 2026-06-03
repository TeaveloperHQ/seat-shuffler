using System.Collections.Generic;
using System.Text.Json.Serialization;
using SeatShuffler.Models;

namespace SeatShuffler.Services;

/// <summary>학생 영속용 평면 DTO. ObservableObject(Student)는 source-gen이
/// 다른 제너레이터의 생성 프로퍼티를 못 봐 직렬화가 비므로 DTO로 매핑한다.</summary>
public sealed class StudentDto
{
    public string StudentNumber { get; set; } = "";
    public string Name { get; set; } = "";
    public Gender Gender { get; set; } = Gender.Unspecified;
}

/// <summary>roster.json 루트 문서.</summary>
public sealed class RosterDocument
{
    public int SchemaVersion { get; set; } = 1;
    public List<StudentDto> Students { get; set; } = new();
}

/// <summary>history.json 루트 문서.</summary>
public sealed class HistoryDocument
{
    public int SchemaVersion { get; set; } = 1;
    public List<ConfirmedRecord> Records { get; set; } = new();
}

/// <summary>constraints.json 루트 문서 — 제약 + 배정 설정.</summary>
public sealed class ConstraintsDocument
{
    public int SchemaVersion { get; set; } = 1;
    public SeatConstraints Constraints { get; set; } = new();
    public AssignmentSettings Settings { get; set; } = new();
}

/// <summary>
/// System.Text.Json source-gen 컨텍스트.
/// 트리밍을 켜더라도 안전하도록 영속 타입을 등록한다.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RosterDocument))]
[JsonSerializable(typeof(HistoryDocument))]
[JsonSerializable(typeof(ConstraintsDocument))]
public partial class AppJsonContext : JsonSerializerContext
{
}
