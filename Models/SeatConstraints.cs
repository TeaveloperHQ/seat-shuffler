using System.Collections.Generic;

namespace SeatShuffler.Models;

/// <summary>사용자가 지정하는 배정 제약(영속). 학생은 식별키로 참조한다.</summary>
public sealed class SeatConstraints
{
    /// <summary>짝이 되면 안 되는 학생 쌍.</summary>
    public List<StudentPair> ForbiddenPairs { get; set; } = new();

    /// <summary>반드시 짝이 되어야 하는 학생 쌍.</summary>
    public List<StudentPair> RequiredPairs { get; set; } = new();

    /// <summary>앞자리에 앉아야 하는 학생 식별키.</summary>
    public List<string> FrontRowStudents { get; set; } = new();

    /// <summary>'앞자리'로 간주하는 앞쪽 행 수(맨 앞부터).</summary>
    public int FrontRowCount { get; set; } = 1;
}
