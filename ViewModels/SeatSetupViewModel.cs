using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeatShuffler.Models;
using SeatShuffler.Services;

namespace SeatShuffler.ViewModels;

/// <summary>좌석 구조(분단·행·열) + 빈자리/남녀 자리 지정. 실시간 미리보기·영속.</summary>
public partial class SeatSetupViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly HashSet<SeatPosition> _emptySeats = new();
    private readonly Dictionary<SeatPosition, Gender> _genderSeats = new();
    private bool _loading;

    [ObservableProperty] private decimal _sections = 3;
    [ObservableProperty] private decimal _rows = 4;
    [ObservableProperty] private decimal _cols = 2;

    // 칠하기 모드 (라디오)
    [ObservableProperty] private bool _modeEmpty = true;
    [ObservableProperty] private bool _modeMale;
    [ObservableProperty] private bool _modeFemale;
    [ObservableProperty] private bool _modeFree;

    [ObservableProperty] private string _status = "";

    public ObservableCollection<SeatSectionViewModel> SectionsView { get; } = new();

    private PaintMode Mode =>
        ModeMale ? PaintMode.Male :
        ModeFemale ? PaintMode.Female :
        ModeFree ? PaintMode.Free : PaintMode.Empty;

    public SeatSetupViewModel(AppState state)
    {
        _state = state;
        LoadFromSettings();
        RenderPreview();
    }

    // 디자인타임용
    public SeatSetupViewModel() : this(new AppState()) { }

    private void LoadFromSettings()
    {
        _loading = true;
        var s = _state.Settings;
        Sections = s.Sections;
        Rows = s.Rows;
        Cols = s.Cols;
        _emptySeats.Clear();
        foreach (var p in s.EmptySeats) _emptySeats.Add(p.ToPosition());
        _genderSeats.Clear();
        foreach (var g in s.GenderSeats) _genderSeats[g.ToPosition()] = g.Gender;
        Prune();
        _loading = false;
    }

    private void SaveSettings()
    {
        if (_loading) return;
        var s = _state.Settings;
        s.Sections = (int)Sections;
        s.Rows = (int)Rows;
        s.Cols = (int)Cols;
        s.EmptySeats = _emptySeats
            .Select(p => new SeatPosDto { Section = p.Section, Row = p.Row, Col = p.Col }).ToList();
        s.GenderSeats = _genderSeats
            .Select(kv => new GenderSeatDto { Section = kv.Key.Section, Row = kv.Key.Row, Col = kv.Key.Col, Gender = kv.Value })
            .ToList();
        _state.SaveConstraints();
    }

    partial void OnSectionsChanged(decimal value) => OnGridShapeChanged();
    partial void OnRowsChanged(decimal value) => OnGridShapeChanged();
    partial void OnColsChanged(decimal value) => OnGridShapeChanged();

    private void OnGridShapeChanged()
    {
        if (_loading) return;
        Prune();
        SaveSettings();
        RenderPreview();
    }

    private void Prune()
    {
        int s = Math.Max(1, (int)Sections), r = Math.Max(1, (int)Rows), c = Math.Max(1, (int)Cols);
        bool OutOfRange(SeatPosition p) => p.Section >= s || p.Row >= r || p.Col >= c;
        _emptySeats.RemoveWhere(OutOfRange);
        foreach (var key in _genderSeats.Keys.Where(OutOfRange).ToList())
            _genderSeats.Remove(key);
    }

    private SeatGridConfig BuildConfig() => new()
    {
        Sections = Math.Max(1, (int)Sections),
        Rows = Math.Max(1, (int)Rows),
        Cols = Math.Max(1, (int)Cols),
    };

    [RelayCommand]
    private void PaintSeat(SeatPosition pos)
    {
        switch (Mode)
        {
            case PaintMode.Empty:
                if (!_emptySeats.Remove(pos)) { _emptySeats.Add(pos); _genderSeats.Remove(pos); }
                break;
            case PaintMode.Male:
                if (_genderSeats.TryGetValue(pos, out var gm) && gm == Gender.Male) _genderSeats.Remove(pos);
                else { _genderSeats[pos] = Gender.Male; _emptySeats.Remove(pos); }
                break;
            case PaintMode.Female:
                if (_genderSeats.TryGetValue(pos, out var gf) && gf == Gender.Female) _genderSeats.Remove(pos);
                else { _genderSeats[pos] = Gender.Female; _emptySeats.Remove(pos); }
                break;
            case PaintMode.Free:
                _emptySeats.Remove(pos);
                _genderSeats.Remove(pos);
                break;
        }
        SaveSettings();
        RenderPreview();
    }

    private void RenderPreview()
    {
        Student? None(SeatPosition _) => null;
        SectionsView.Clear();
        foreach (var sec in SeatGridBuilder.Build(BuildConfig(), None, _emptySeats, PaintSeatCommand, _genderSeats))
            SectionsView.Add(sec);

        int total = BuildConfig().TotalSeats;
        int male = _genderSeats.Count(kv => kv.Value == Gender.Male);
        int female = _genderSeats.Count(kv => kv.Value == Gender.Female);
        Status = $"좌석 {total - _emptySeats.Count}석 (빈자리 {_emptySeats.Count}) · 남자리 {male} · 여자리 {female} " +
                 "— 모드를 고르고 좌석을 클릭하세요.";
    }

    /// <summary>탭 진입 시 호출 — 설정이 외부에서 바뀌었을 수 있어 다시 로드·렌더.</summary>
    public void Refresh()
    {
        LoadFromSettings();
        RenderPreview();
    }
}
