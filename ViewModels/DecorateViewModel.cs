using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeatShuffler.Services;

namespace SeatShuffler.ViewModels;

/// <summary>꾸미기 탭: 출력 스킨·교탁 기준 + 미리보기 + PNG 출력.</summary>
public partial class DecorateViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly IExportService _export;
    private bool _loading;

    public ChartSkin[] Skins { get; } = ChartSkin.Presets.ToArray();

    [ObservableProperty] private ChartSkin? _selectedSkin;
    [ObservableProperty] private bool _flipForTeacher;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private IBrush _previewBackground = Brushes.White;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private bool _canExport;

    public ObservableCollection<SeatSectionViewModel> PreviewSections { get; } = new();

    public DecorateViewModel(AppState state, IExportService export)
    {
        _state = state;
        _export = export;
        LoadFromSettings();
        RenderPreview();
    }

    // 디자인타임용
    public DecorateViewModel() : this(new AppState(), new UiServices()) { }

    private void LoadFromSettings()
    {
        _loading = true;
        SelectedSkin = ChartSkin.ById(_state.Settings.SkinId);
        FlipForTeacher = _state.Settings.FlipForTeacher;
        _loading = false;
    }

    private void SaveSettings()
    {
        if (_loading) return;
        _state.Settings.SkinId = SelectedSkin?.Id ?? "basic";
        _state.Settings.FlipForTeacher = FlipForTeacher;
        _state.SaveConstraints();
    }

    partial void OnSelectedSkinChanged(ChartSkin? value) { SaveSettings(); RenderPreview(); }
    partial void OnFlipForTeacherChanged(bool value) { SaveSettings(); RenderPreview(); }

    private void RenderPreview()
    {
        var skin = SelectedSkin ?? ChartSkin.Presets[0];
        PreviewBackground = skin.PageBackground;

        PreviewSections.Clear();
        var snap = _state.LastChart;
        if (snap is null)
        {
            CanExport = false;
            Status = "'배정' 탭에서 먼저 자리를 배정하세요. 그 결과가 여기에 표시됩니다.";
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
        if (_state.LastChart is null) return;
        await _export.ExportSeatChartAsync(
            "자리 배치표", _state.LastChart, SelectedSkin ?? ChartSkin.Presets[0], FlipForTeacher);
    }

    /// <summary>탭 진입 시 호출 — 최신 배정 결과로 미리보기 갱신.</summary>
    public void Refresh()
    {
        LoadFromSettings();
        RenderPreview();
    }
}
