using System.Collections.Generic;

namespace SeatShuffler.Models;

/// <summary>배정 후보 1건. 확정 전 임시 결과.</summary>
public sealed class AssignmentCandidate
{
    public required SeatGridConfig Config { get; init; }

    /// <summary>좌석 → 학생키. 학생키가 없으면(키 미포함) 빈 자리.</summary>
    public required IReadOnlyDictionary<SeatPosition, string> SeatToStudentKey { get; init; }

    public long Seed { get; init; }
    public RelaxationReport Relaxation { get; init; } = new();
}
