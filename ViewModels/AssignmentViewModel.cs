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

    [ObservableProperty] private decimal _sections = 3;
    [ObservableProperty] private decimal _rows = 4;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PairOptionsEnabled))]
    private decimal _cols = 2;

    [ObservableProperty] private bool _pairSame = true;
    [ObservableProperty] private bool _pairOpposite;
    [ObservableProperty] private bool _avoidSameSeat = true;
    [ObservableProperty] private bool _avoidSamePair = true;

    [ObservableProperty] private string _status = "분단·행·열을 정하고 '배정'을 누르세요.";
    [ObservableProperty] private string _relaxationBanner = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private bool _canConfirm;

    public ObservableCollection<SeatSectionViewModel> SectionsView { get; } = new();

    public bool PairOptionsEnabled => (int)Cols >= 2;
    public bool HasRelaxation => !string.IsNullOrEmpty(RelaxationBanner);

    public AssignmentViewModel(AppState state, SeatAssignmentService service)
    {
        _state = state;
        _service = service;
    }

    // 디자인타임용
    public AssignmentViewModel() : this(new AppState(), new SeatAssignmentService()) { }

    partial void OnPairSameChanged(bool value) { if (value) PairOpposite = false; }
    partial void OnPairOppositeChanged(bool value) { if (value) PairSame = false; }
    partial void OnRelaxationBannerChanged(string value) => OnPropertyChanged(nameof(HasRelaxation));

    private SeatGridConfig BuildConfig() => new()
    {
        Sections = Math.Max(1, (int)Sections),
        Rows = Math.Max(1, (int)Rows),
        Cols = Math.Max(1, (int)Cols),
        PairMode = PairOpposite ? PairMode.OppositeGender : PairMode.SameGender,
    };

    [RelayCommand]
    private void Assign()
    {
        var config = BuildConfig();
        var options = new AssignmentOptions
        {
            AvoidSameSeat = AvoidSameSeat,
            AvoidSamePair = AvoidSamePair,
        };
        long seed = _rng.NextInt64();

        var result = _service.Assign(_state.Roster.ToList(), config, options, _state.History.ToList(), seed);
        if (!result.Ok)
        {
            Status = result.Error ?? "배정 실패";
            CanConfirm = false;
            SectionsView.Clear();
            RelaxationBanner = "";
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
                Section = pos.Section,
                Row = pos.Row,
                Col = pos.Col,
            });
        }

        _state.History.Add(record);
        _state.SaveHistory();
        Status = $"확정 완료 — 기록 탭에 저장됨 ({record.Label}).";
        CanConfirm = false;
    }

    private void RenderCandidate(AssignmentCandidate candidate)
    {
        var byKey = _state.Roster.ToDictionary(s => s.Key, s => s);
        Student? Lookup(SeatPosition pos) =>
            candidate.SeatToStudentKey.TryGetValue(pos, out var key) && byKey.TryGetValue(key, out var s)
                ? s : null;

        SectionsView.Clear();
        foreach (var sec in SeatGridBuilder.Build(candidate.Config, Lookup))
            SectionsView.Add(sec);
    }
}
