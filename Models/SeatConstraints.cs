using System.Collections.Generic;
using System.Linq;

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

    /// <summary>특정 학생을 특정 좌석에 고정.</summary>
    public List<SeatPin> FixedSeats { get; set; } = new();

    /// <summary>특정 학생이 특정 좌석을 회피(절대 안 앉음).</summary>
    public List<SeatPin> AvoidedSeats { get; set; } = new();

    /// <summary>제약(4종) 충돌 시 완화 우선순위(앞=높음=나중에 완화). 비어 있으면 기본값.</summary>
    public List<ConstraintKind> Priority { get; set; } = new();

    /// <summary>사용자 조정 대상인 제약 4종만, 정규화(누락분 보강).</summary>
    public List<ConstraintKind> ConstraintPriority()
    {
        var ordered = Priority.Where(ConstraintKindInfo.ConstraintKinds.Contains).Distinct().ToList();
        foreach (var k in ConstraintKindInfo.ConstraintKinds)
            if (!ordered.Contains(k)) ordered.Add(k);
        return ordered;
    }

    /// <summary>솔버용 전체 우선순위: 제약(사용자 순서) 다음에 옵션(고정·가장 낮음).</summary>
    public List<ConstraintKind> EffectivePriority()
    {
        var list = ConstraintPriority();
        list.AddRange(ConstraintKindInfo.OptionKinds);
        return list;
    }
}
