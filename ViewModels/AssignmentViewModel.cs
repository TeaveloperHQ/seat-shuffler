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
    private bool _loading;

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
        PairOpposite = s.PairMode == PairMode.OppositeGender;
        PairSame = !PairOpposite;
        AvoidSameSeat = s.AvoidSameSeat;
        AvoidSamePair = s.AvoidSamePair;
        _loading = false;
    }

    private void SaveSettings()
    {
        if (_loading) return;
        var s = _state.Settings;
        s.PairMode = PairOpposite ? PairMode.OppositeGender : PairMode.SameGender;
        s.AvoidSameSeat = AvoidSameSeat;
        s.AvoidSamePair = AvoidSamePair;
        _state.SaveConstraints();
    }

    partial void OnPairSameChanged(bool value) { if (value) PairOpposite = false; SaveSettings(); }
    partial void OnPairOppositeChanged(bool value) { if (value) PairSame = false; SaveSettings(); }
    partial void OnAvoidSameSeatChanged(bool value) => SaveSettings();
    partial void OnAvoidSamePairChanged(bool value) => SaveSettings();
    partial void OnRelaxationBannerChanged(string value) => OnPropertyChanged(nameof(HasRelaxation));

    private SeatGridConfig BuildConfig() => new()
    {
        Sections = Math.Max(1, _state.Settings.Sections),
        Rows = Math.Max(1, _state.Settings.Rows),
        Cols = Math.Max(1, _state.Settings.Cols),
        PairMode = PairOpposite ? PairMode.OppositeGender : PairMode.SameGender,
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
            RelaxationBanner = "";
            RenderPreview();
            return;
        }

        _candidate = result.Candidate!;
        RenderCandidate(_candidate);

        int placed = _candidate.SeatToStudentKey.Count;
        Status = $"{placed}명 배정됨 · 분단{config.Sections}·행{config.Rows}·열{config.Cols}" +
                 (config.HasPairs ? $" · {(config.PairMode == PairMode.OppositeGender ? "이성짝" : "동성짝")}" : " · 단독석");
        RelaxationBanner = _candidate.Relaxation.Summary;
        CanConfirm = true;
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
        foreach (var (pos, key) in _candidate.SeatToStudentKey)
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

    private void RenderCandidate(AssignmentCandidate candidate)
    {
        var byKey = _state.Roster.ToDictionary(s => s.Key, s => s);
        Student? Lookup(SeatPosition pos) =>
            candidate.SeatToStudentKey.TryGetValue(pos, out var key) && byKey.TryGetValue(key, out var s)
                ? s : null;

        SectionsView.Clear();
        foreach (var sec in SeatGridBuilder.Build(candidate.Config, Lookup, EmptySeats(), null, GenderSeats()))
            SectionsView.Add(sec);
    }

    /// <summary>탭 진입 시 호출 — 좌석 설정이 바뀌었을 수 있어 다시 반영.</summary>
    public void Refresh()
    {
        LoadFromSettings();
        _candidate = null;
        CanConfirm = false;
        RelaxationBanner = "";
        OnPropertyChanged(nameof(PairOptionsEnabled));
        RenderPreview();
    }
}
