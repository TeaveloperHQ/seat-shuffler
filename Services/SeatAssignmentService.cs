using System;
using System.Collections.Generic;
using System.Linq;
using SeatShuffler.Models;

namespace SeatShuffler.Services;

public sealed class AssignmentOptions
{
    public bool AvoidSameSeat { get; init; }
    public bool AvoidSamePair { get; init; }
}

public sealed class AssignmentResult
{
    public AssignmentCandidate? Candidate { get; init; }
    public string? Error { get; init; }
    public bool Ok => Candidate is not null;
}

/// <summary>
/// 제약 기반 좌석 배정. 랜덤 백트래킹 + 완화 사다리.
/// 완화 순서(먼저 완화 → 나중): 성별 짝 → 같은자리 회피 → 같은짝 회피 →
/// 앞자리 → 짝 금지 → 짝 필수. (사용자 제약이 성별·기록 회피보다 우선)
/// 빈자리는 구조적 제외라 완화 대상이 아니다. 최하단은 항상 해가 존재해 종료를 보장한다.
/// </summary>
public sealed class SeatAssignmentService
{
    private const int NodeBudget = 400_000;

    private readonly record struct Constraints(
        bool Gender, bool SameSeat, bool SamePair, bool FrontRow, bool GenderSeat,
        bool Forbidden, bool Required);

    public AssignmentResult Assign(
        IReadOnlyList<Student> roster,
        SeatGridConfig config,
        AssignmentOptions options,
        IReadOnlyList<ConfirmedRecord> history,
        SeatConstraints constraints,
        IReadOnlySet<SeatPosition> emptySeats,
        IReadOnlyDictionary<SeatPosition, Gender> genderSeats,
        long seed)
    {
        var students = roster.Where(s => s.HasName).ToList();
        var layout = SeatLayout.Build(config, emptySeats);

        if (students.Count > layout.SeatCount)
            return new AssignmentResult
            {
                Error = $"배정 가능 좌석({layout.SeatCount})보다 학생({students.Count})이 많습니다. " +
                        "분단·행·열을 늘리거나 빈자리를 줄이세요."
            };

        if (students.Count == 0)
            return new AssignmentResult { Error = "명단이 비어 있습니다. '학생 명단' 탭에서 학생을 추가하세요." };

        var keys = students.Select(s => s.Key).ToHashSet();

        var forbiddenSeat = BuildForbiddenSeat(history);
        var historyPair = BuildForbiddenPair(history);
        var manualForbidden = constraints.ForbiddenPairs.Select(p => p.Key).ToHashSet();
        var requiredPartner = BuildRequiredPartners(constraints.RequiredPairs, keys);
        var frontRowKeys = constraints.FrontRowStudents.Where(keys.Contains).ToHashSet();
        int frontRowCount = Math.Max(1, constraints.FrontRowCount);

        // 현재 좌석 범위 내의 남녀 자리만 채택.
        var seatPositions = layout.Positions.ToHashSet();
        var effectiveGenderSeats = genderSeats
            .Where(kv => seatPositions.Contains(kv.Key) && kv.Value != Gender.Unspecified)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        bool hasGender = config.HasPairs;
        bool hasFront = frontRowKeys.Count > 0;
        bool hasGenderSeat = effectiveGenderSeats.Count > 0;
        bool hasForbidden = manualForbidden.Count > 0;
        bool hasRequired = requiredPartner.Count > 0;

        var top = new Constraints(
            Gender: hasGender,
            SameSeat: options.AvoidSameSeat,
            SamePair: options.AvoidSamePair,
            FrontRow: hasFront,
            GenderSeat: hasGenderSeat,
            Forbidden: hasForbidden,
            Required: hasRequired);

        foreach (var level in BuildLadder(top, constraints.NormalizedPriority()))
        {
            var solver = new Solver(students, layout, level, config.PairMode,
                forbiddenSeat, historyPair, manualForbidden, requiredPartner,
                frontRowKeys, frontRowCount, effectiveGenderSeats, seed, NodeBudget);

            if (solver.Solve(out var seatToKey))
            {
                var report = new RelaxationReport
                {
                    RelaxedGenderPairing = hasGender && !level.Gender,
                    RelaxedSameSeat = options.AvoidSameSeat && !level.SameSeat,
                    RelaxedSamePair = options.AvoidSamePair && !level.SamePair,
                    RelaxedFrontRow = hasFront && !level.FrontRow,
                    RelaxedGenderSeat = hasGenderSeat && !level.GenderSeat,
                    RelaxedForbiddenPair = hasForbidden && !level.Forbidden,
                    RelaxedRequiredPair = hasRequired && !level.Required,
                };
                return new AssignmentResult
                {
                    Candidate = new AssignmentCandidate
                    {
                        Config = config.Clone(),
                        SeatToStudentKey = seatToKey,
                        Seed = seed,
                        Relaxation = report,
                    }
                };
            }
        }

        return new AssignmentResult { Error = "배정에 실패했습니다. 구성을 확인하세요." };
    }

    // 사용자 우선순위(앞=높음)에 따라 완화 사다리 구성: 낮은 순위(뒤)부터 차례로 해제.
    private static List<Constraints> BuildLadder(Constraints top, IReadOnlyList<ConstraintKind> priority)
    {
        var levels = new List<Constraints> { top };
        var c = top;
        for (int i = priority.Count - 1; i >= 0; i--)
        {
            var k = priority[i];
            if (!IsOn(c, k)) continue;
            c = TurnOff(c, k);
            levels.Add(c);
        }
        return levels;
    }

    private static bool IsOn(Constraints c, ConstraintKind k) => k switch
    {
        ConstraintKind.GenderPairing => c.Gender,
        ConstraintKind.SameSeat => c.SameSeat,
        ConstraintKind.SamePair => c.SamePair,
        ConstraintKind.FrontRow => c.FrontRow,
        ConstraintKind.GenderSeat => c.GenderSeat,
        ConstraintKind.ForbiddenPair => c.Forbidden,
        ConstraintKind.RequiredPair => c.Required,
        _ => false,
    };

    private static Constraints TurnOff(Constraints c, ConstraintKind k) => k switch
    {
        ConstraintKind.GenderPairing => c with { Gender = false },
        ConstraintKind.SameSeat => c with { SameSeat = false },
        ConstraintKind.SamePair => c with { SamePair = false },
        ConstraintKind.FrontRow => c with { FrontRow = false },
        ConstraintKind.GenderSeat => c with { GenderSeat = false },
        ConstraintKind.ForbiddenPair => c with { Forbidden = false },
        ConstraintKind.RequiredPair => c with { Required = false },
        _ => c,
    };

    private static Dictionary<string, string> BuildRequiredPartners(
        IEnumerable<StudentPair> required, HashSet<string> rosterKeys)
    {
        var map = new Dictionary<string, string>();
        foreach (var p in required)
        {
            if (p.A == p.B) continue;
            if (!rosterKeys.Contains(p.A) || !rosterKeys.Contains(p.B)) continue; // 둘 다 명단에 있어야
            // 한 학생이 여러 필수 짝을 가지면 충돌 → 첫 지정만 채택.
            if (map.ContainsKey(p.A) || map.ContainsKey(p.B)) continue;
            map[p.A] = p.B;
            map[p.B] = p.A;
        }
        return map;
    }

    private static HashSet<(string, string)> BuildForbiddenSeat(IReadOnlyList<ConfirmedRecord> history)
    {
        var set = new HashSet<(string, string)>();
        foreach (var rec in history)
            foreach (var p in rec.Placements)
                set.Add((p.StudentKey, p.Position.Key));
        return set;
    }

    private static HashSet<string> BuildForbiddenPair(IReadOnlyList<ConfirmedRecord> history)
    {
        var set = new HashSet<string>();
        foreach (var rec in history)
        {
            var byPos = rec.Placements.ToDictionary(p => p.Position.Key, p => p.StudentKey);
            foreach (var p in rec.Placements)
            {
                if (p.Col % 2 != 0) continue;
                var rightKey = new SeatPosition(p.Section, p.Row, p.Col + 1).Key;
                if (byPos.TryGetValue(rightKey, out var partner))
                    set.Add(PairKey(p.StudentKey, partner));
            }
        }
        return set;
    }

    internal static string PairKey(string a, string b)
        => string.CompareOrdinal(a, b) <= 0 ? $"{a} {b}" : $"{b} {a}";

    /// <summary>한 완화 레벨에 대한 백트래킹 솔버.</summary>
    private sealed class Solver
    {
        private readonly Student[] _students;
        private readonly bool[] _used;
        private readonly IReadOnlyList<PairGroup> _groups;
        private readonly Constraints _c;
        private readonly PairMode _pairMode;
        private readonly HashSet<(string, string)> _forbiddenSeat;
        private readonly HashSet<string> _historyPair;
        private readonly HashSet<string> _manualForbidden;
        private readonly Dictionary<string, string> _requiredPartner;
        private readonly HashSet<string> _frontRowKeys;
        private readonly int _frontRowCount;
        private readonly Dictionary<SeatPosition, Gender> _genderSeats;
        private readonly int _budget;
        private readonly Dictionary<SeatPosition, string> _placed = new();
        private int _nodes;
        private int _remaining;

        public Solver(
            IReadOnlyList<Student> students, SeatLayout layout, Constraints c, PairMode pairMode,
            HashSet<(string, string)> forbiddenSeat, HashSet<string> historyPair,
            HashSet<string> manualForbidden, Dictionary<string, string> requiredPartner,
            HashSet<string> frontRowKeys, int frontRowCount,
            Dictionary<SeatPosition, Gender> genderSeats, long seed, int budget)
        {
            var rng = new Random(unchecked((int)seed));
            // 제약이 강한 학생(앞자리·짝필수)을 먼저 배치하도록 우선순위 정렬 →
            // 비제약 학생이 한정 좌석을 선점해 생기는 깊은 백트래킹을 줄인다.
            bool Priority(Student s) => frontRowKeys.Contains(s.Key) || requiredPartner.ContainsKey(s.Key);
            var shuffled = students.ToArray();
            Shuffle(shuffled, rng);
            _students = shuffled.Where(Priority).Concat(shuffled.Where(s => !Priority(s))).ToArray();

            // 앞자리 제약이 있으면 앞줄 그룹을 먼저 처리(앞 좌석 선점). 없으면 순수 무작위 유지.
            var groups = layout.Pairs.ToArray();
            Shuffle(groups, rng);
            _groups = frontRowKeys.Count > 0 ? groups.OrderBy(g => g.A.Row).ToArray() : groups;
            _used = new bool[_students.Length];
            _remaining = _students.Length;
            _c = c;
            _pairMode = pairMode;
            _forbiddenSeat = forbiddenSeat;
            _historyPair = historyPair;
            _manualForbidden = manualForbidden;
            _requiredPartner = requiredPartner;
            _frontRowKeys = frontRowKeys;
            _frontRowCount = frontRowCount;
            _genderSeats = genderSeats;
            _budget = budget;
        }

        public bool Solve(out IReadOnlyDictionary<SeatPosition, string> result)
        {
            var ok = Backtrack(0);
            result = _placed;
            return ok;
        }

        private bool Backtrack(int gi)
        {
            if (_remaining == 0) return true;
            if (++_nodes > _budget) return false;
            if (gi >= _groups.Count) return false;

            var g = _groups[gi];

            if (g.IsSingle)
            {
                for (int i = 0; i < _students.Length; i++)
                {
                    if (_used[i]) continue;
                    if (!CanSeatSingle(_students[i], g.A)) continue;
                    Place(g.A, i);
                    if (Backtrack(gi + 1)) return true;
                    Unplace(g.A, i);
                }
                return Backtrack(gi + 1); // 비우고 진행
            }

            var b = g.B!.Value;
            for (int i = 0; i < _students.Length; i++)
            {
                if (_used[i]) continue;
                var sa = _students[i];
                if (!CanSeat(sa, g.A)) continue;
                Place(g.A, i);

                for (int j = 0; j < _students.Length; j++)
                {
                    if (j == i || _used[j]) continue;
                    var sb = _students[j];
                    if (!CanSeat(sb, b)) continue;
                    if (!CanPair(sa, sb)) continue;
                    Place(b, j);
                    if (Backtrack(gi + 1)) return true;
                    Unplace(b, j);
                }

                // 오른쪽 비우고 진행 — 단, 짝 필수 학생은 단독 배치 불가.
                if (!(_c.Required && _requiredPartner.ContainsKey(sa.Key)))
                    if (Backtrack(gi + 1)) return true;

                Unplace(g.A, i);
            }

            return Backtrack(gi + 1); // 짝 전체 비우고 진행
        }

        // 일반 좌석 배치 가능 여부(같은자리 회피 + 앞자리 + 남녀 자리).
        private bool CanSeat(Student s, SeatPosition pos)
        {
            if (_c.SameSeat && _forbiddenSeat.Contains((s.Key, pos.Key))) return false;
            if (_c.FrontRow && _frontRowKeys.Contains(s.Key) && pos.Row >= _frontRowCount) return false;
            if (_c.GenderSeat && _genderSeats.TryGetValue(pos, out var g) && s.Gender != g) return false;
            return true;
        }

        // 단독석 배치: 짝 필수 학생은 단독 불가.
        private bool CanSeatSingle(Student s, SeatPosition pos)
        {
            if (_c.Required && _requiredPartner.ContainsKey(s.Key)) return false;
            return CanSeat(s, pos);
        }

        private bool CanPair(Student a, Student b)
        {
            if (_c.Required)
            {
                // 한쪽이 짝 필수면 상대가 반드시 지정 파트너여야 한다.
                if (_requiredPartner.TryGetValue(a.Key, out var pa) && pa != b.Key) return false;
                if (_requiredPartner.TryGetValue(b.Key, out var pb) && pb != a.Key) return false;
            }
            if (_c.Forbidden && _manualForbidden.Contains(PairKey(a.Key, b.Key))) return false;
            if (_c.SamePair && _historyPair.Contains(PairKey(a.Key, b.Key))) return false;
            if (_c.Gender && !GenderOk(a.Gender, b.Gender)) return false;
            return true;
        }

        private bool GenderOk(Gender a, Gender b)
            => _pairMode == PairMode.SameGender
                ? a == b || a == Gender.Unspecified || b == Gender.Unspecified
                : a != b && a != Gender.Unspecified && b != Gender.Unspecified;

        private void Place(SeatPosition pos, int idx)
        {
            _placed[pos] = _students[idx].Key;
            _used[idx] = true;
            _remaining--;
        }

        private void Unplace(SeatPosition pos, int idx)
        {
            _placed.Remove(pos);
            _used[idx] = false;
            _remaining++;
        }

        private static void Shuffle<T>(T[] arr, Random rng)
        {
            for (int i = arr.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
        }
    }
}
