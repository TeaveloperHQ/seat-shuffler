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

    /// <summary>제약을 통째로 버리기 전에 시도해 볼 '예외' 최대 수(1건씩 늘려가며 재시도).</summary>
    private const int MaxExceptions = 8;

    /// <summary>최소 완화를 찾는 추가 탐색에 쓸 시간 한도 — 넘으면 지금까지의 답으로 마무리.</summary>
    private const int RefineMillis = 1200;

    /// <summary>예외를 허용할 개수(0이면 엄격). 셋 다 '기록·성별' 같은 소프트 제약이다.</summary>
    private readonly record struct SoftBudget(int Gender, int SameSeat, int SamePair)
    {
        public static readonly SoftBudget Strict = new(0, 0, 0);
        public SoftBudget With(ConstraintKind kind, int n) => kind switch
        {
            ConstraintKind.GenderPairing => this with { Gender = n },
            ConstraintKind.SameSeat => this with { SameSeat = n },
            _ => this with { SamePair = n },
        };
    }

    /// <summary>예외를 몇 건 썼는지(해를 찾은 뒤 보고용).</summary>
    private readonly record struct SoftUsed(int Gender, int SameSeat, int SamePair);

    /// <summary>예외로 눈감아 줄 수 있는 제약 — 나머지는 켜거나 끄거나 둘 중 하나.</summary>
    private static bool SupportsExceptions(ConstraintKind k) =>
        k is ConstraintKind.GenderPairing or ConstraintKind.SameSeat or ConstraintKind.SamePair;

    private readonly record struct Constraints(
        bool Gender, bool SameSeat, bool SamePair, bool FrontRow, bool GenderSeat,
        bool Forbidden, bool Required, bool Fixed, bool Avoid);

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

        // 명단·좌석과 맞지 않아 버려지는 제약을 세어 사용자에게 알린다(조용히 사라지면 오해를 부른다).
        int ghostRefs = 0, offGridPins = 0, duplicateRequired = 0;

        var forbiddenSeat = BuildForbiddenSeat(history);
        var historyPair = BuildForbiddenPair(history);

        // 가급적 멀리: '같은 짝만 금지'는 짝 그룹 단위, 그 이상은 거리(Chebyshev) 단위.
        var forbiddenDeskmate = new HashSet<string>();
        var apartDist = new Dictionary<string, List<(string Partner, int MinDist)>>();
        foreach (var p in constraints.ForbiddenPairs)
        {
            if (p.A == p.B || !keys.Contains(p.A) || !keys.Contains(p.B)) { ghostRefs++; continue; }
            int d = DistanceLevels.ApartMinDistance(p.Level);
            if (d < 0) forbiddenDeskmate.Add(PairKey(p.A, p.B));
            else { AddDist(apartDist, p.A, p.B, d); AddDist(apartDist, p.B, p.A, d); }
        }

        // 가급적 가깝게: '반드시 짝'은 짝 그룹 단위, '가까이'는 거리 단위.
        var requiredPartner = new Dictionary<string, string>();
        var requiredDeskmates = new List<(string A, string B)>(); // '반드시 짝'인 쌍(사전 충돌 검사용)
        var closeDist = new Dictionary<string, List<(string Partner, int MaxDist)>>();
        foreach (var p in constraints.RequiredPairs)
        {
            if (p.A == p.B || !keys.Contains(p.A) || !keys.Contains(p.B)) { ghostRefs++; continue; }
            int d = DistanceLevels.CloseMaxDistance(p.Level);
            if (d < 0)
            {
                // 한 학생에게 '반드시 짝'이 둘 이상이면 앞선 것만 살린다.
                if (requiredPartner.ContainsKey(p.A) || requiredPartner.ContainsKey(p.B)) { duplicateRequired++; continue; }
                requiredPartner[p.A] = p.B;
                requiredPartner[p.B] = p.A;
                requiredDeskmates.Add((p.A, p.B));
            }
            else { AddDist(closeDist, p.A, p.B, d); AddDist(closeDist, p.B, p.A, d); }
        }

        var frontRowKeys = constraints.FrontRowStudents.Where(keys.Contains).ToHashSet();
        ghostRefs += constraints.FrontRowStudents.Count - frontRowKeys.Count;
        int frontRowCount = Math.Max(1, constraints.FrontRowCount);

        // 현재 좌석 범위 내의 남녀 자리만 채택.
        var seatPositions = layout.Positions.ToHashSet();
        var effectiveGenderSeats = genderSeats
            .Where(kv => seatPositions.Contains(kv.Key) && kv.Value != Gender.Unspecified)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        // 자리 고정: 학생→좌석, 좌석→학생 (충돌·범위밖·빈자리 무시).
        var fixedOf = new Dictionary<string, SeatPosition>();
        var fixedAt = new Dictionary<SeatPosition, string>();
        foreach (var pin in constraints.FixedSeats)
        {
            var pos = pin.Position;
            if (!keys.Contains(pin.StudentKey)) { ghostRefs++; continue; }
            if (!seatPositions.Contains(pos)) { offGridPins++; continue; } // 범위 밖이거나 '비움' 좌석
            if (fixedOf.ContainsKey(pin.StudentKey) || fixedAt.ContainsKey(pos)) continue;
            fixedOf[pin.StudentKey] = pos;
            fixedAt[pos] = pin.StudentKey;
        }

        // 자리 회피: 학생→피할 좌석들.
        var avoidOf = new Dictionary<string, HashSet<SeatPosition>>();
        foreach (var pin in constraints.AvoidedSeats)
        {
            var pos = pin.Position;
            if (!keys.Contains(pin.StudentKey)) { ghostRefs++; continue; }
            if (!seatPositions.Contains(pos)) { offGridPins++; continue; }
            if (!avoidOf.TryGetValue(pin.StudentKey, out var set)) { set = new(); avoidOf[pin.StudentKey] = set; }
            set.Add(pos);
        }

        bool hasGender = config.HasPairs && config.PairMode != PairMode.Any; // 무작위면 성별 조건 없음
        // 기록이 없으면 회피할 대상도 없다 — 사다리에 넣으면 '완화했다'는 헛배너가 뜬다.
        bool hasSameSeat = options.AvoidSameSeat && forbiddenSeat.Count > 0;
        bool hasSamePair = options.AvoidSamePair && historyPair.Count > 0;
        bool hasFront = frontRowKeys.Count > 0;
        bool hasGenderSeat = effectiveGenderSeats.Count > 0;
        bool hasForbidden = forbiddenDeskmate.Count > 0 || apartDist.Count > 0;
        bool hasRequired = requiredPartner.Count > 0 || closeDist.Count > 0;
        bool hasFixed = fixedOf.Count > 0;
        bool hasAvoid = avoidOf.Count > 0;

        var top = new Constraints(
            Gender: hasGender,
            SameSeat: hasSameSeat,
            SamePair: hasSamePair,
            FrontRow: hasFront,
            GenderSeat: hasGenderSeat,
            Forbidden: hasForbidden,
            Required: hasRequired,
            Fixed: hasFixed,
            Avoid: hasAvoid);

        // '반드시 짝'인 쌍이 다른 제약과 정면으로 부딪히면 탐색할 것도 없이 불가능하다.
        // (400k 노드를 헛돌지 않게 하는 사전 검사 — 필요조건만 보므로 해를 놓치지 않는다.)
        var genderOf = students.ToDictionary(x => x.Key, x => x.Gender);
        bool Hopeless(Constraints level, SoftBudget budget)
        {
            if (!level.Required || requiredDeskmates.Count == 0) return false;
            int samePairHits = 0, genderHits = 0;
            foreach (var (a, b) in requiredDeskmates)
            {
                var key = PairKey(a, b);
                if (level.Forbidden && forbiddenDeskmate.Contains(key)) return true; // 짝 필수 ↔ 짝 금지
                if (level.SamePair && historyPair.Contains(key)) samePairHits++;      // 짝 필수 ↔ 같은 짝 회피
                if (level.Gender && !GenderPairOk(genderOf[a], genderOf[b], config.PairMode)) genderHits++;
            }
            return samePairHits > budget.SamePair || genderHits > budget.Gender;
        }

        // 한 조합(제약 on/off + 예외 허용치)으로 배치를 시도한다.
        bool Solve(Constraints level, SoftBudget budget,
            out IReadOnlyDictionary<SeatPosition, string> seatToKey, out SoftUsed used)
        {
            if (Hopeless(level, budget))
            {
                seatToKey = new Dictionary<SeatPosition, string>();
                used = default;
                return false;
            }

            var solver = new Solver(students, layout, level, config.PairMode,
                forbiddenSeat, historyPair, forbiddenDeskmate, apartDist, requiredPartner, closeDist,
                frontRowKeys, frontRowCount, effectiveGenderSeats, fixedOf, fixedAt, avoidOf,
                config.Cols, seed, NodeBudget, budget.Gender, budget.SameSeat, budget.SamePair);
            bool ok = solver.Solve(out seatToKey);
            used = solver.Used;
            return ok;
        }

        // 꺼진 제약을 우선순위 높은 순으로 되살려 본다. 되살아나면 그 조합을 채택.
        (Constraints, SoftBudget, IReadOnlyDictionary<SeatPosition, string>, SoftUsed) Refine(
            Constraints level, SoftBudget budget, IReadOnlyDictionary<SeatPosition, string> best, SoftUsed used,
            Constraints top, IReadOnlyList<ConstraintKind> priority)
        {
            // 답은 이미 손에 있으므로, 다듬기에는 별도의 시간 예산을 준다.
            var clock = System.Diagnostics.Stopwatch.StartNew();
            foreach (var kind in priority)
            {
                if (clock.ElapsedMilliseconds > RefineMillis) break;
                if (!IsOn(top, kind) || IsOn(level, kind)) continue; // 원래 없었거나 이미 켜져 있음

                var candidate = TurnOn(level, kind);
                if (Solve(candidate, budget, out var seats, out var u))
                {
                    (level, best, used) = (candidate, seats, u);
                    continue;
                }

                // 통째로는 안 되지만 예외 몇 건이면 지킬 수 있는 제약은 그렇게라도 살린다.
                if (!SupportsExceptions(kind)) continue;
                for (int ex = 1; ex <= MaxExceptions; ex++)
                {
                    if (clock.ElapsedMilliseconds > RefineMillis) break;
                    var b2 = budget.With(kind, ex);
                    if (!Solve(candidate, b2, out var seats2, out var u2)) continue;
                    (level, budget, best, used) = (candidate, b2, seats2, u2);
                    break;
                }
            }
            return (level, budget, best, used);
        }

        var priority = constraints.EffectivePriority();
        var ladder = BuildLadder(top, priority);

        // ① 우선순위가 낮은 제약부터 차례로 풀어 가며 '되는 단계'를 먼저 찾는다(항상 빠르게 답 확보).
        Constraints level = default;
        IReadOnlyDictionary<SeatPosition, string>? seats = null;
        var budget = SoftBudget.Strict;
        var used = default(SoftUsed);
        foreach (var candidate in ladder)
            if (Solve(candidate, SoftBudget.Strict, out seats, out used)) { level = candidate; break; }

        if (seats is null)
            return new AssignmentResult { Error = "배정에 실패했습니다. 구성을 확인하세요." };

        // ② 사다리는 낮은 우선순위부터 통째로 껐다 — 실제로는 안 꺼도 되는 것이 섞여 있다.
        //    남는 시간 안에서 다시 켜 보고, 통째로는 안 되면 '예외 n건'만 두고 지킨다(= 최소 완화).
        (level, budget, seats, used) = Refine(level, budget, seats, used, top, priority);

        return new AssignmentResult
        {
            Candidate = new AssignmentCandidate
            {
                Config = config.Clone(),
                SeatToStudentKey = seats,
                Seed = seed,
                Relaxation = new RelaxationReport
                {
                    RelaxedGenderPairing = hasGender && !level.Gender,
                    RelaxedSameSeat = hasSameSeat && !level.SameSeat,
                    RelaxedSamePair = hasSamePair && !level.SamePair,
                    RelaxedFrontRow = hasFront && !level.FrontRow,
                    RelaxedGenderSeat = hasGenderSeat && !level.GenderSeat,
                    RelaxedForbiddenPair = hasForbidden && !level.Forbidden,
                    RelaxedRequiredPair = hasRequired && !level.Required,
                    RelaxedFixedSeat = hasFixed && !level.Fixed,
                    RelaxedAvoidSeat = hasAvoid && !level.Avoid,
                    GenderPairExceptions = used.Gender,
                    SameSeatExceptions = used.SameSeat,
                    SamePairExceptions = used.SamePair,
                },
                IgnoredNotices = BuildIgnoredNotices(ghostRefs, offGridPins, duplicateRequired),
            }
        };
    }

    private static List<string> BuildIgnoredNotices(int ghostRefs, int offGridPins, int duplicateRequired)
    {
        var notices = new List<string>();
        if (ghostRefs > 0)
            notices.Add($"명단에 없는 학생을 가리키는 제약 {ghostRefs}건은 무시했습니다(명단에서 지웠거나 학번·이름이 바뀐 경우).");
        if (offGridPins > 0)
            notices.Add($"지금 좌석에 없는 자리를 가리키는 고정·회피 {offGridPins}건은 무시했습니다(분단·행·열 밖이거나 '비움' 좌석).");
        if (duplicateRequired > 0)
            notices.Add($"한 학생에게 '반드시 짝'이 겹쳐 지정된 {duplicateRequired}건은 무시했습니다.");
        return notices;
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
        ConstraintKind.FixedSeat => c.Fixed,
        ConstraintKind.AvoidSeat => c.Avoid,
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
        ConstraintKind.FixedSeat => c with { Fixed = false },
        ConstraintKind.AvoidSeat => c with { Avoid = false },
        _ => c,
    };

    private static Constraints TurnOn(Constraints c, ConstraintKind k) => k switch
    {
        ConstraintKind.GenderPairing => c with { Gender = true },
        ConstraintKind.SameSeat => c with { SameSeat = true },
        ConstraintKind.SamePair => c with { SamePair = true },
        ConstraintKind.FrontRow => c with { FrontRow = true },
        ConstraintKind.GenderSeat => c with { GenderSeat = true },
        ConstraintKind.ForbiddenPair => c with { Forbidden = true },
        ConstraintKind.RequiredPair => c with { Required = true },
        ConstraintKind.FixedSeat => c with { Fixed = true },
        ConstraintKind.AvoidSeat => c with { Avoid = true },
        _ => c,
    };

    private static void AddDist(
        Dictionary<string, List<(string Partner, int Dist)>> map, string a, string b, int dist)
    {
        if (!map.TryGetValue(a, out var list)) { list = new(); map[a] = list; }
        list.Add((b, dist));
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

    /// <summary>짝 성별 규칙 충족 여부. 미지정은 동성짝에서 와일드카드, 이성짝에서는 부적격.</summary>
    private static bool GenderPairOk(Gender a, Gender b, PairMode mode) => mode switch
    {
        PairMode.SameGender => a == b || a == Gender.Unspecified || b == Gender.Unspecified,
        PairMode.OppositeGender => a != b && a != Gender.Unspecified && b != Gender.Unspecified,
        _ => true, // Any: 동성·이성 무작위
    };

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
        private readonly HashSet<string> _forbiddenDeskmate;
        private readonly Dictionary<string, List<(string Partner, int MinDist)>> _apartDist;
        private readonly Dictionary<string, string> _requiredPartner;
        private readonly Dictionary<string, List<(string Partner, int MaxDist)>> _closeDist;
        private readonly HashSet<string> _frontRowKeys;
        private readonly int _frontRowCount;
        private readonly Dictionary<SeatPosition, Gender> _genderSeats;
        private readonly Dictionary<string, SeatPosition> _fixedOf;
        private readonly Dictionary<SeatPosition, string> _fixedAt;
        private readonly Dictionary<string, HashSet<SeatPosition>> _avoidOf;
        private readonly int _cols;
        private readonly int _budget;
        private readonly int _genderExceptionBudget;
        private readonly int _seatExceptionBudget;
        private readonly int _pairExceptionBudget;
        private int _genderExceptions;
        private int _seatExceptions;
        private int _pairExceptions;
        private readonly Dictionary<SeatPosition, string> _placed = new();
        private readonly Dictionary<string, SeatPosition> _posOf = new();
        private int _nodes;
        private int _remaining;

        public Solver(
            IReadOnlyList<Student> students, SeatLayout layout, Constraints c, PairMode pairMode,
            HashSet<(string, string)> forbiddenSeat, HashSet<string> historyPair,
            HashSet<string> forbiddenDeskmate, Dictionary<string, List<(string, int)>> apartDist,
            Dictionary<string, string> requiredPartner, Dictionary<string, List<(string, int)>> closeDist,
            HashSet<string> frontRowKeys, int frontRowCount,
            Dictionary<SeatPosition, Gender> genderSeats,
            Dictionary<string, SeatPosition> fixedOf, Dictionary<SeatPosition, string> fixedAt,
            Dictionary<string, HashSet<SeatPosition>> avoidOf,
            int cols, long seed, int budget,
            int genderExceptionBudget, int seatExceptionBudget, int pairExceptionBudget)
        {
            var rng = new Random(unchecked((int)seed));
            // 제약이 강한 학생(앞자리·짝필수·거리·고정·회피)을 먼저 배치 → 깊은 백트래킹 감소.
            bool Priority(Student s) => frontRowKeys.Contains(s.Key) || requiredPartner.ContainsKey(s.Key)
                || apartDist.ContainsKey(s.Key) || closeDist.ContainsKey(s.Key)
                || fixedOf.ContainsKey(s.Key) || avoidOf.ContainsKey(s.Key);
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
            _forbiddenDeskmate = forbiddenDeskmate;
            _apartDist = apartDist;
            _requiredPartner = requiredPartner;
            _closeDist = closeDist;
            _frontRowKeys = frontRowKeys;
            _frontRowCount = frontRowCount;
            _genderSeats = genderSeats;
            _fixedOf = fixedOf;
            _fixedAt = fixedAt;
            _avoidOf = avoidOf;
            _cols = cols;
            _budget = budget;
            _genderExceptionBudget = genderExceptionBudget;
            _seatExceptionBudget = seatExceptionBudget;
            _pairExceptionBudget = pairExceptionBudget;
        }

        /// <summary>해를 찾았을 때 실제로 쓴 예외 수(그 제약이 꺼진 레벨에서는 0).</summary>
        public SoftUsed Used => new(_genderExceptions, _seatExceptions, _pairExceptions);

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
            if (gi >= _groups.Count) return false; // 남은 좌석보다 학생이 많음 — 더 볼 것 없음

            var g = _groups[gi];

            if (g.IsSingle)
            {
                for (int i = 0; i < _students.Length; i++)
                {
                    if (_used[i]) continue;
                    if (!CanSeatSingle(_students[i], g.A, out bool seatEx)) continue;
                    Place(g.A, i);
                    if (seatEx) _seatExceptions++;
                    if (Backtrack(gi + 1)) return true;
                    if (seatEx) _seatExceptions--;
                    Unplace(g.A, i);
                }
                return Backtrack(gi + 1); // 비우고 진행
            }

            var b = g.B!.Value;
            for (int i = 0; i < _students.Length; i++)
            {
                if (_used[i]) continue;
                var sa = _students[i];
                if (!CanSeat(sa, g.A, out bool seatExA)) continue;
                Place(g.A, i);
                if (seatExA) _seatExceptions++; // 짝꿍 후보를 보기 전에 반영해야 예산이 맞다

                for (int j = 0; j < _students.Length; j++)
                {
                    if (j == i || _used[j]) continue;
                    var sb = _students[j];
                    if (!CanSeat(sb, b, out bool seatExB)) continue;
                    if (!CanPair(sa, sb, out bool genderEx, out bool pairEx)) continue;
                    Place(b, j);
                    if (seatExB) _seatExceptions++;
                    if (genderEx) _genderExceptions++;
                    if (pairEx) _pairExceptions++;
                    if (Backtrack(gi + 1)) return true;
                    if (pairEx) _pairExceptions--;
                    if (genderEx) _genderExceptions--;
                    if (seatExB) _seatExceptions--;
                    Unplace(b, j);
                }

                // 오른쪽 비우고 진행 — 단, 짝 필수 학생은 단독 배치 불가.
                if (!(_c.Required && _requiredPartner.ContainsKey(sa.Key)))
                    if (Backtrack(gi + 1)) return true;

                if (seatExA) _seatExceptions--;
                Unplace(g.A, i);
            }

            return Backtrack(gi + 1); // 짝 전체 비우고 진행
        }

        // 일반 좌석 배치 가능 여부(같은자리 회피 + 앞자리 + 남녀 자리 + 거리 제약).
        // seatException: 같은 자리 회피를 예산 안에서 어기고 앉히는 경우.
        private bool CanSeat(Student s, SeatPosition pos, out bool seatException)
        {
            seatException = false;
            if (_c.SameSeat && _forbiddenSeat.Contains((s.Key, pos.Key)))
            {
                if (_seatExceptions >= _seatExceptionBudget) return false;
                seatException = true;
            }
            if (_c.FrontRow && _frontRowKeys.Contains(s.Key) && pos.Row >= _frontRowCount) return false;
            if (_c.GenderSeat && _genderSeats.TryGetValue(pos, out var g) && s.Gender != g) return false;

            // 자리 고정: 이 학생은 지정 좌석에만, 이 좌석은 지정 학생만.
            if (_c.Fixed)
            {
                if (_fixedOf.TryGetValue(s.Key, out var fp) && !fp.Equals(pos)) return false;
                if (_fixedAt.TryGetValue(pos, out var owner) && owner != s.Key) return false;
            }
            // 자리 회피: 이 학생이 피해야 할 좌석.
            if (_c.Avoid && _avoidOf.TryGetValue(s.Key, out var av) && av.Contains(pos)) return false;

            // 가급적 멀리: 이미 배치된 상대와 최소 거리 확보.
            if (_c.Forbidden && _apartDist.TryGetValue(s.Key, out var aps))
                foreach (var (partner, minDist) in aps)
                    if (_posOf.TryGetValue(partner, out var pp) && Chebyshev(pos, pp) < minDist) return false;

            // 가급적 가깝게: 이미 배치된 상대와 최대 거리 이내.
            if (_c.Required && _closeDist.TryGetValue(s.Key, out var cls))
                foreach (var (partner, maxDist) in cls)
                    if (_posOf.TryGetValue(partner, out var pp) && Chebyshev(pos, pp) > maxDist) return false;

            return true;
        }

        // 교실 전체를 한 격자로 보고 Chebyshev(체비셰프) 거리. 팔방 인접 = 1.
        private int Chebyshev(SeatPosition a, SeatPosition b)
        {
            int ga = a.Section * _cols + a.Col, gb = b.Section * _cols + b.Col;
            return Math.Max(Math.Abs(a.Row - b.Row), Math.Abs(ga - gb));
        }

        // 단독석 배치: 짝 필수 학생은 단독 불가.
        private bool CanSeatSingle(Student s, SeatPosition pos, out bool seatException)
        {
            seatException = false;
            if (_c.Required && _requiredPartner.ContainsKey(s.Key)) return false;
            return CanSeat(s, pos, out seatException);
        }

        private bool CanPair(Student a, Student b, out bool genderException, out bool pairException)
        {
            genderException = false;
            pairException = false;
            if (_c.Required)
            {
                // 한쪽이 짝 필수면 상대가 반드시 지정 파트너여야 한다.
                if (_requiredPartner.TryGetValue(a.Key, out var pa) && pa != b.Key) return false;
                if (_requiredPartner.TryGetValue(b.Key, out var pb) && pb != a.Key) return false;
            }
            if (_c.Forbidden && _forbiddenDeskmate.Contains(PairKey(a.Key, b.Key))) return false;
            if (_c.SamePair && _historyPair.Contains(PairKey(a.Key, b.Key)))
            {
                if (_pairExceptions >= _pairExceptionBudget) return false;
                pairException = true;
            }
            if (_c.Gender && !GenderOk(a.Gender, b.Gender))
            {
                // 성별 규칙 위반은 예산(예외 짝 수) 안에서만 허용 — 0이면 종전과 같이 금지.
                if (_genderExceptions >= _genderExceptionBudget) return false;
                genderException = true;
            }
            return true;
        }

        private bool GenderOk(Gender a, Gender b) => GenderPairOk(a, b, _pairMode);

        private void Place(SeatPosition pos, int idx)
        {
            _placed[pos] = _students[idx].Key;
            _posOf[_students[idx].Key] = pos;
            _used[idx] = true;
            _remaining--;
        }

        private void Unplace(SeatPosition pos, int idx)
        {
            _placed.Remove(pos);
            _posOf.Remove(_students[idx].Key);
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
