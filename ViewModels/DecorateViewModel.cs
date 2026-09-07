using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeatShuffler.Models;
using SeatShuffler.Services;

namespace SeatShuffler.ViewModels;

/// <summary>좌석표 글꼴 선택지. 목록 항목을 그 글꼴로 미리 보여준다.</summary>
public sealed class ChartFontOption
{
    /// <summary>설정에 저장하는 값(빈 문자열이면 기본 글꼴).</summary>
    public string Value { get; init; } = "";
    public string Display { get; init; } = "";
    public FontFamily Preview { get; init; } = FontFamily.Default;
}

/// <summary>출력 차트 소스(현재 배정 또는 기록).</summary>
public sealed class ChartSource
{
    public string Label { get; init; } = "";
    public ChartSnapshot? Snapshot { get; init; }
}

/// <summary>꾸미기 탭: 소스 선택(현재 배정/기록) + 스킨·교탁 기준 + 지면 미리보기 + 인쇄.</summary>
public partial class DecorateViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly UiServices _ui;
    private Bitmap? _backgroundBitmap;
    private Bitmap? _maleCellBitmap;
    private Bitmap? _femaleCellBitmap;
    private bool _loading;

    public ChartSkin[] Skins { get; } = ChartSkin.Presets.ToArray();

    /// <summary>기본 글꼴 + 이 컴퓨터에 설치된 글꼴 목록.</summary>
    public ChartFontOption[] Fonts { get; } = BuildFontList();
    public ObservableCollection<ChartSource> Sources { get; } = new();

    [ObservableProperty] private ChartSource? _selectedSource;
    [ObservableProperty] private ChartSkin? _selectedSkin;
    [ObservableProperty] private ChartFontOption? _selectedFont;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BoardAtTop))]
    [NotifyPropertyChangedFor(nameof(BoardAtBottom))]
    private bool _flipForTeacher;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BoardAtTop))]
    [NotifyPropertyChangedFor(nameof(BoardAtBottom))]
    private bool _showBoard = true;

    [ObservableProperty] private bool _transparentCells;
    [ObservableProperty] private bool _genderColors = true;
    [ObservableProperty] private string _status = "";
    /// <summary>인쇄될 것과 똑같은 좌석표 비주얼(같은 빌더로 만든다).</summary>
    [ObservableProperty] private Control? _previewChart;
    [ObservableProperty] private bool _hasBackground;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnyCellImage))]
    private bool _hasMaleCell;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnyCellImage))]
    private bool _hasFemaleCell;

    /// <summary>'셀 투명 배경'은 셀 이미지에만 적용된다 — 이미지가 없으면 켤 수 없다.</summary>
    public bool HasAnyCellImage => HasMaleCell || HasFemaleCell;

    // 미리보기 종이 = 실제 인쇄 지면(A4 가로)과 같은 비율·여백. 값은 PageLayout 한 곳에서 온다.
    public double PageWidth => PageLayout.PageWidthPt;
    public double PageHeight => PageLayout.PageHeightPt;
    public Thickness PageMargin => new(PageLayout.MarginXPt, PageLayout.MarginYPt);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrintCommand))]
    private bool _canPrint;

    // 칠판 표시 + 교탁 기준이면 아래쪽에(인쇄물과 동일).
    public bool BoardAtTop => ShowBoard && !FlipForTeacher;
    public bool BoardAtBottom => ShowBoard && FlipForTeacher;

    public DecorateViewModel(AppState state, UiServices ui)
    {
        _state = state;
        _ui = ui;
        LoadFromSettings();
        BuildSources();
        RenderPreview();
    }

    // 디자인타임용
    public DecorateViewModel() : this(new AppState(), new UiServices()) { }

    private static ChartFontOption[] BuildFontList()
    {
        var list = new List<ChartFontOption>
        {
            new() { Value = "", Display = "기본 글꼴", Preview = UiServices.DefaultChartFont },
        };
        try
        {
            // 설치된 글꼴은 앱이 초기화된 뒤에만 읽을 수 있다(디자이너·테스트에서는 실패할 수 있음).
            foreach (var name in FontManager.Current.SystemFonts.Select(f => f.Name).Distinct().OrderBy(n => n, StringComparer.CurrentCulture))
                list.Add(new ChartFontOption { Value = name, Display = name, Preview = new FontFamily(name) });
        }
        catch { /* 목록을 못 읽으면 기본 글꼴만 제공 */ }
        return list.ToArray();
    }

    private void LoadFromSettings()
    {
        _loading = true;
        SelectedSkin = ChartSkin.ById(_state.Settings.SkinId);
        FlipForTeacher = _state.Settings.FlipForTeacher;
        ShowBoard = _state.Settings.ShowBoard;
        TransparentCells = _state.Settings.TransparentCells;
        GenderColors = _state.Settings.GenderColors;
        SelectedFont = Fonts.FirstOrDefault(f => f.Value == _state.Settings.ChartFontFamily) ?? Fonts[0];
        ReloadImages();
        _loading = false;
    }

    private static Bitmap? TryLoad(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try { return new Bitmap(path); } catch { return null; }
    }

    private void ReloadImages()
    {
        _backgroundBitmap = TryLoad(_state.Settings.BackgroundImagePath);
        var male = TryLoad(_state.Settings.MaleCellImagePath);
        var female = TryLoad(_state.Settings.FemaleCellImagePath);
        // 투명 배경 모드면 불투명 이미지의 모서리 배경색을 제거(누끼).
        if (TransparentCells)
        {
            if (male is not null) male = ImageBackground.RemoveCornerBackground(male);
            if (female is not null) female = ImageBackground.RemoveCornerBackground(female);
        }
        _maleCellBitmap = male;
        _femaleCellBitmap = female;
        HasBackground = _backgroundBitmap is not null;
        HasMaleCell = _maleCellBitmap is not null;
        HasFemaleCell = _femaleCellBitmap is not null;
    }

    private async Task PickInto(System.Action<string?> set)
    {
        var path = await _ui.PickImageAsync();
        if (path is null) return;
        set(path);
        _state.SaveConstraints();
        ReloadImages();
        RenderPreview();
    }

    private void ClearImage(System.Action set)
    {
        set();
        _state.SaveConstraints();
        ReloadImages();
        RenderPreview();
    }

    [RelayCommand] private Task LoadBackgroundAsync() => PickInto(p => _state.Settings.BackgroundImagePath = p);
    [RelayCommand] private void ClearBackground() => ClearImage(() => _state.Settings.BackgroundImagePath = null);
    [RelayCommand] private Task LoadMaleCellAsync() => PickInto(p => _state.Settings.MaleCellImagePath = p);
    [RelayCommand] private void ClearMaleCell() => ClearImage(() => _state.Settings.MaleCellImagePath = null);
    [RelayCommand] private Task LoadFemaleCellAsync() => PickInto(p => _state.Settings.FemaleCellImagePath = p);
    [RelayCommand] private void ClearFemaleCell() => ClearImage(() => _state.Settings.FemaleCellImagePath = null);

    private void SaveSettings()
    {
        if (_loading) return;
        _state.Settings.SkinId = SelectedSkin?.Id ?? "basic";
        _state.Settings.FlipForTeacher = FlipForTeacher;
        _state.Settings.ShowBoard = ShowBoard;
        _state.Settings.TransparentCells = TransparentCells;
        _state.Settings.GenderColors = GenderColors;
        _state.Settings.ChartFontFamily = SelectedFont?.Value ?? "";
        _state.SaveConstraints();
    }

    // 소스 목록: '현재 배정' + 기록(최신 먼저). 선택은 가능하면 유지.
    private void BuildSources()
    {
        var prevLabel = SelectedSource?.Label;
        Sources.Clear();
        Sources.Add(new ChartSource { Label = "현재 배정 (배정 탭)", Snapshot = _state.LastChart });
        foreach (var rec in _state.History.Reverse())
            Sources.Add(new ChartSource
            {
                Label = $"기록 · {rec.Label} ({rec.ConfigSummary}, {rec.StudentCount}명)",
                Snapshot = FromRecord(rec),
            });

        _loading = true;
        SelectedSource = Sources.FirstOrDefault(s => s.Label == prevLabel) ?? Sources[0];
        _loading = false;
    }

    private static ChartSnapshot FromRecord(ConfirmedRecord rec)
    {
        var snap = new ChartSnapshot { Config = rec.Config.Clone() };
        foreach (var p in rec.Placements)
            snap.Seats.Add(new ChartSeat
            {
                Section = p.Section, Row = p.Row, Col = p.Col,
                Name = p.StudentName,
                SubText = p.StudentKey != p.StudentName ? p.StudentKey : p.Gender.ToKorean(),
                Gender = p.Gender,
            });
        return snap;
    }

    partial void OnSelectedSkinChanged(ChartSkin? value) { SaveSettings(); RenderPreview(); }
    partial void OnFlipForTeacherChanged(bool value) { SaveSettings(); RenderPreview(); }
    partial void OnShowBoardChanged(bool value) { SaveSettings(); RenderPreview(); }
    partial void OnTransparentCellsChanged(bool value) { SaveSettings(); ReloadImages(); RenderPreview(); }
    partial void OnGenderColorsChanged(bool value) { SaveSettings(); RenderPreview(); }
    partial void OnSelectedFontChanged(ChartFontOption? value) { SaveSettings(); RenderPreview(); }
    partial void OnSelectedSourceChanged(ChartSource? value) { if (!_loading) RenderPreview(); }

    private void RenderPreview()
    {
        var skin = SelectedSkin ?? ChartSkin.Presets[0];
        var snap = SelectedSource?.Snapshot;
        if (snap is null)
        {
            PreviewChart = null;
            CanPrint = false;
            Status = "'배정' 탭에서 배정하거나, 위에서 기록을 선택하세요.";
            return;
        }

        // 인쇄와 완전히 같은 경로로 만든다 — 종이 미리보기에 보이는 그대로 인쇄된다.
        var sections = ChartBuilder.Build(snap, skin, FlipForTeacher, _maleCellBitmap, _femaleCellBitmap,
            TransparentCells, GenderColors);
        PreviewChart = UiServices.BuildChart(UiServices.ChartTitle, sections, skin, FlipForTeacher,
            _backgroundBitmap, ShowBoard, SelectedFont?.Value);
        CanPrint = true;
        Status = FlipForTeacher
            ? "교탁 기준(좌석 거울반전). 아래 미리보기가 인쇄될 A4 가로 한 장입니다."
            : "아래 미리보기가 인쇄될 A4 가로 한 장입니다. '인쇄'를 누르면 프린터를 고를 수 있습니다.";
    }

    private bool CanPrintExec() => CanPrint;

    /// <summary>지금 화면 설정 그대로의 렌더 옵션.</summary>
    private ChartRenderOptions? CurrentRenderOptions() =>
        SelectedSource?.Snapshot is not { } snap
            ? null
            : new ChartRenderOptions(snap, SelectedSkin ?? ChartSkin.Presets[0], FlipForTeacher,
                _backgroundBitmap, _maleCellBitmap, _femaleCellBitmap, TransparentCells, ShowBoard,
                GenderColors, SelectedFont?.Value);

    /// <summary>인쇄 대화상자를 띄운다 — 실패해도 앱이 죽지 않게 사유를 안내 문구로 보여준다.</summary>
    [RelayCommand(CanExecute = nameof(CanPrintExec))]
    private void Print()
    {
        if (CurrentRenderOptions() is not { } options) return;
        try
        {
            Status = _ui.PrintSeatChart(UiServices.ChartTitle, options) is { } printer
                ? $"'{printer}'(으)로 보냈습니다. 인쇄를 확인하세요."
                : "인쇄를 취소했습니다.";
        }
        catch (Exception ex)
        {
            Status = $"인쇄하지 못했습니다 — {ex.Message}";
        }
    }

    /// <summary>탭 진입 시 호출 — 소스 목록(최신 기록 포함)·미리보기 갱신.</summary>
    public void Refresh()
    {
        LoadFromSettings();
        BuildSources();
        RenderPreview();
    }
}
