using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeatShuffler.Models;
using SeatShuffler.Services;

namespace SeatShuffler.ViewModels;

/// <summary>출력 차트 소스(현재 배정 또는 기록).</summary>
public sealed class ChartSource
{
    public string Label { get; init; } = "";
    public ChartSnapshot? Snapshot { get; init; }
}

/// <summary>꾸미기 탭: 소스 선택(현재 배정/기록) + 스킨·교탁 기준 + 미리보기 + PNG 출력.</summary>
public partial class DecorateViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly UiServices _ui;
    private Bitmap? _backgroundBitmap;
    private bool _loading;

    public ChartSkin[] Skins { get; } = ChartSkin.Presets.ToArray();
    public ObservableCollection<ChartSource> Sources { get; } = new();

    [ObservableProperty] private ChartSource? _selectedSource;
    [ObservableProperty] private ChartSkin? _selectedSkin;
    [ObservableProperty] private bool _flipForTeacher;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private IBrush _previewBackground = Brushes.White;
    [ObservableProperty] private Bitmap? _previewBackgroundImage;
    [ObservableProperty] private bool _hasBackground;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private bool _canExport;

    public ObservableCollection<SeatSectionViewModel> PreviewSections { get; } = new();

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

    private void LoadFromSettings()
    {
        _loading = true;
        SelectedSkin = ChartSkin.ById(_state.Settings.SkinId);
        FlipForTeacher = _state.Settings.FlipForTeacher;
        LoadBackgroundBitmap(_state.Settings.BackgroundImagePath);
        _loading = false;
    }

    private void LoadBackgroundBitmap(string? path)
    {
        _backgroundBitmap = null;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            try { _backgroundBitmap = new Bitmap(path); } catch { _backgroundBitmap = null; }
        }
        PreviewBackgroundImage = _backgroundBitmap;
        HasBackground = _backgroundBitmap is not null;
    }

    [RelayCommand]
    private async Task LoadBackgroundAsync()
    {
        var path = await _ui.PickImageAsync();
        if (path is null) return;
        _state.Settings.BackgroundImagePath = path;
        _state.SaveConstraints();
        LoadBackgroundBitmap(path);
        RenderPreview();
    }

    [RelayCommand]
    private void ClearBackground()
    {
        _state.Settings.BackgroundImagePath = null;
        _state.SaveConstraints();
        LoadBackgroundBitmap(null);
        RenderPreview();
    }

    private void SaveSettings()
    {
        if (_loading) return;
        _state.Settings.SkinId = SelectedSkin?.Id ?? "basic";
        _state.Settings.FlipForTeacher = FlipForTeacher;
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
    partial void OnSelectedSourceChanged(ChartSource? value) { if (!_loading) RenderPreview(); }

    private void RenderPreview()
    {
        var skin = SelectedSkin ?? ChartSkin.Presets[0];
        PreviewBackground = skin.PageBackground;

        PreviewSections.Clear();
        var snap = SelectedSource?.Snapshot;
        if (snap is null)
        {
            CanExport = false;
            Status = "'배정' 탭에서 배정하거나, 위에서 기록을 선택하세요.";
            return;
        }

        foreach (var sec in ChartBuilder.Build(snap, skin, FlipForTeacher))
            PreviewSections.Add(sec);
        CanExport = true;
        Status = FlipForTeacher
            ? "교탁 기준(좌석 거울반전). '출력'으로 PNG 저장 후 인쇄하세요."
            : "'출력'으로 PNG 저장 후 인쇄하세요.";
    }

    private bool CanExportExec() => CanExport;

    [RelayCommand(CanExecute = nameof(CanExportExec))]
    private async Task ExportAsync()
    {
        var snap = SelectedSource?.Snapshot;
        if (snap is null) return;
        await _ui.ExportSeatChartAsync(
            "자리 배치표", snap, SelectedSkin ?? ChartSkin.Presets[0], FlipForTeacher, _backgroundBitmap);
    }

    /// <summary>탭 진입 시 호출 — 소스 목록(최신 기록 포함)·미리보기 갱신.</summary>
    public void Refresh()
    {
        LoadFromSettings();
        BuildSources();
        RenderPreview();
    }
}
