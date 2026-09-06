using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeatShuffler.Models;
using SeatShuffler.Services;

namespace SeatShuffler.ViewModels;

public partial class AssignmentViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly SeatAssignmentService _service;
    private readonly Random _rng = new();
    private AssignmentCandidate? _candidate;
    private readonly Dictionary<SeatPosition, string> _assignment = new(); // 표시·수동교체용 현재 배치
    private SeatPosition? _pendingSwap;
    private bool _loading;

    // 두 체크박스: 둘 다 켜면 동성·이성 무작위. 최소 하나는 켜져 있어야 한다.
    [ObservableProperty] private bool _pairSame = true;
    [ObservableProperty] private bool _pairOpposite;
    [ObservableProperty] private bool _avoidSameSeat = true;
    [ObservableProperty] private bool _avoidSamePair = true;

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _relaxationBanner = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private bool _canConfirm;

    public ObservableCollection<SeatSectionViewModel> SectionsView { get; } = new();

    public bool PairOptionsEnabled => _state.Settings.Cols >= 2;

    /// <summary>체크 조합 → 짝 성별 규칙. 둘 다 켜짐 = 무작위.</summary>
    private PairMode CurrentPairMode =>
        PairSame && PairOpposite ? PairMode.Any :
        PairOpposite ? PairMode.OppositeGender : PairMode.SameGender;

    private static string PairModeLabel(PairMode mode) => mode switch
    {
        PairMode.OppositeGender => "이성짝",
        PairMode.Any => "동성/이성 무작위",
        _ => "동성짝",
    };

    public bool HasRelaxation => !string.IsNullOrEmpty(RelaxationBanner);

    public AssignmentViewModel(AppState state, SeatAssignmentService service)
    {
        _state = state;
        _service = service;
        LoadFromSettings();
        RenderPreview();
    }

    // 디자인타임용
    public AssignmentViewModel() : this(new AppState(), new SeatAssignmentService()) { }

    private void LoadFromSettings()
    {
        _loading = true;
        var s = _state.Settings;
        PairSame = s.PairMode is PairMode.SameGender or PairMode.Any;
        PairOpposite = s.PairMode is PairMode.OppositeGender or PairMode.Any;
        AvoidSameSeat = s.AvoidSameSeat;
        AvoidSamePair = s.AvoidSamePair;
        _loading = false;
    }

    private void SaveSettings()
    {
        if (_loading) return;
        var s = _state.Settings;
        s.PairMode = CurrentPairMode;
        s.AvoidSameSeat = AvoidSameSeat;
        s.AvoidSamePair = AvoidSamePair;
        _state.SaveConstraints();
    }

    // 마지막 하나까지 끄면 짝을 만들 수 없으므로 되돌린다.
    partial void OnPairSameChanged(bool value)
    {
        if (_loading) return;
        if (!value && !PairOpposite) { PairSame = true; return; }
        SaveSettings();
    }

    partial void OnPairOppositeChanged(bool value)
    {
        if (_loading) return;
        if (!value && !PairSame) { PairOpposite = true; return; }
        SaveSettings();
    }

    partial void OnAvoidSameSeatChanged(bool value) => SaveSettings();
    partial void OnAvoidSamePairChanged(bool value) => SaveSettings();
    partial void OnRelaxationBannerChanged(string value) => OnPropertyChanged(nameof(HasRelaxation));

    private SeatGridConfig BuildConfig() => new()
    {
        Sections = Math.Max(1, _state.Settings.Sections),
        Rows = Math.Max(1, _state.Settings.Rows),
        Cols = Math.Max(1, _state.Settings.Cols),
        PairMode = CurrentPairMode,
    };

    private HashSet<SeatPosition> EmptySeats() =>
        _state.Settings.EmptySeats.Select(p => p.ToPosition()).ToHashSet();

    private Dictionary<SeatPosition, Gender> GenderSeats() =>
        _state.Settings.GenderSeats.ToDictionary(g => g.ToPosition(), g => g.Gender);

    [RelayCommand]
    private void Assign()
    {
        var config = BuildConfig();
        var options = new AssignmentOptions { AvoidSameSeat = AvoidSameSeat, AvoidSamePair = AvoidSamePair };
        long seed = _rng.NextInt64();

        var result = _service.Assign(
            _state.Roster.ToList(), config, options, _state.History.ToList(),
            _state.Constraints, EmptySeats(), GenderSeats(), seed);

        if (!result.Ok)
        {
            Status = result.Error ?? "배정 실패";
            CanConfirm = false;
            _candidate = null;
            _assignment.Clear();
            _pendingSwap = null;
            _state.LastChart = null;
            RelaxationBanner = "";
            RenderPreview();
            return;
        }

        _candidate = result.Candidate!;
        _assignment.Clear();
        foreach (var (pos, key) in _candidate.SeatToStudentKey) _assignment[pos] = key;
        _pendingSwap = null;
        RenderAssignment();

        int placed = _assignment.Count;
        Status = $"{placed}명 배정됨 · 분단{config.Sections}·행{config.Rows}·열{config.Cols}" +
                 (config.HasPairs ? $" · {PairModeLabel(config.PairMode)}" : " · 단독석") +
                 " · 좌석 둘을 클릭하면 수동 교체";
        RelaxationBanner = _candidate.Relaxation.Summary;
        CanConfirm = true;
    }

    /// <summary>수동 교체: 좌석 둘을 클릭하면 서로 맞바꾼다.</summary>
    [RelayCommand]
    private void SwapSeat(SeatPosition pos)
    {
        if (_candidate is null) return;                 // 배정 후에만 동작
        if (EmptySeats().Contains(pos)) return;         // 비움(배정 제외) 좌석은 교체 대상 아님

        if (_pendingSwap is null)
        {
            _pendingSwap = pos;                         // 첫 좌석 선택
        }
        else if (_pendingSwap.Value.Equals(pos))
        {
            _pendingSwap = null;                        // 같은 좌석 다시 클릭 → 선택 해제
        }
        else
        {
            var a = _pendingSwap.Value;
            _assignment.TryGetValue(a, out var sa);
            _assignment.TryGetValue(pos, out var sb);
            if (sb is null) _assignment.Remove(a); else _assignment[a] = sb;
            if (sa is null) _assignment.Remove(pos); else _assignment[pos] = sa;
            _pendingSwap = null;
        }
        RenderAssignment();
    }

    private bool CanConfirmExec() => CanConfirm;

    [RelayCommand(CanExecute = nameof(CanConfirmExec))]
    private void Confirm()
    {
        if (_candidate is null) return;

        var byKey = _state.Roster.ToDictionary(s => s.Key, s => s);
        var record = new ConfirmedRecord
        {
            ConfirmedAt = DateTimeOffset.Now,
            Label = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm"),
            Config = _candidate.Config.Clone(),
        };
        foreach (var (pos, key) in _assignment)
        {
            byKey.TryGetValue(key, out var s);
            record.Placements.Add(new PlacementDto
            {
                StudentKey = key,
                StudentName = s?.Name ?? key,
                Gender = s?.Gender ?? Gender.Unspecified,
                Section = pos.Section, Row = pos.Row, Col = pos.Col,
            });
        }

        _state.History.Add(record);
        _state.SaveHistory();
        Status = $"확정 완료 — 기록 탭에 저장됨 ({record.Label}).";
        CanConfirm = false;
    }

    private void RenderPreview()
    {
        Student? None(SeatPosition _) => null;
        SectionsView.Clear();
        foreach (var sec in SeatGridBuilder.Build(BuildConfig(), None, EmptySeats(), null, GenderSeats()))
            SectionsView.Add(sec);

        var cfg = BuildConfig();
        int avail = cfg.TotalSeats - EmptySeats().Count;
        Status = $"좌석 {avail}석 · '배정'을 누르세요. (좌석 구조는 '좌석 설정' 탭에서 변경)";
    }

    private void RenderAssignment()
    {
        var byKey = _state.Roster.ToDictionary(s => s.Key, s => s);
        Student? Lookup(SeatPosition pos) =>
            _assignment.TryGetValue(pos, out var key) && byKey.TryGetValue(key, out var s) ? s : null;

        SectionsView.Clear();
        foreach (var sec in SeatGridBuilder.Build(
                     _candidate!.Config, Lookup, EmptySeats(), SwapSeatCommand, GenderSeats(), _pendingSwap))
            SectionsView.Add(sec);

        _state.LastChart = BuildSnapshot();
    }

    private ChartSnapshot BuildSnapshot()
    {
        var byKey = _state.Roster.ToDictionary(s => s.Key, s => s);
        var snap = new ChartSnapshot
        {
            Config = _candidate!.Config.Clone(),
            Empties = EmptySeats(),
            GenderSeats = GenderSeats(),
        };
        foreach (var (pos, key) in _assignment)
        {
            byKey.TryGetValue(key, out var s);
            snap.Seats.Add(new ChartSeat
            {
                Section = pos.Section, Row = pos.Row, Col = pos.Col,
                Name = s?.Name ?? key,
                SubText = s is null ? ""
                    : (string.IsNullOrWhiteSpace(s.StudentNumber) ? s.Gender.ToKorean() : s.StudentNumber),
                Gender = s?.Gender ?? Gender.Unspecified,
            });
        }
        return snap;
    }

    /// <summary>탭 진입 시 호출 — 좌석 설정이 바뀌었을 수 있어 다시 반영.</summary>
    public void Refresh()
    {
        LoadFromSettings();
        _candidate = null;
        _assignment.Clear();
        _pendingSwap = null;
        CanConfirm = false;
        _state.LastChart = null;
        RelaxationBanner = "";
        OnPropertyChanged(nameof(PairOptionsEnabled));
        RenderPreview();
    }
}
