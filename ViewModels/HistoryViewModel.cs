using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeatShuffler.Models;
using SeatShuffler.Services;

namespace SeatShuffler.ViewModels;

public partial class HistoryViewModel : ViewModelBase
{
    private readonly AppState _state;

    public ObservableCollection<ConfirmedRecord> Records => _state.History;

    [ObservableProperty] private ConfirmedRecord? _selectedRecord;
    [ObservableProperty] private string _status = "";

    public ObservableCollection<SeatSectionViewModel> PreviewView { get; } = new();

    public HistoryViewModel(AppState state)
    {
        _state = state;
        UpdateStatus();
        // 탭을 열면 가장 최근 확정 기록을 바로 미리보기.
        SelectedRecord = _state.History.Count > 0 ? _state.History[^1] : null;
    }

    // 디자인타임용
    public HistoryViewModel() : this(new AppState()) { }

    partial void OnSelectedRecordChanged(ConfirmedRecord? value) => RenderPreview(value);

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedRecord is null) return;
        Records.Remove(SelectedRecord);
        _state.SaveHistory();
        SelectedRecord = null;
        PreviewView.Clear();
        UpdateStatus();
    }

    [RelayCommand]
    private void SaveChanges()
    {
        _state.SaveHistory();
        Status = "변경 사항 저장됨.";
    }

    [RelayCommand]
    private void ClearAll()
    {
        Records.Clear();
        _state.SaveHistory();
        PreviewView.Clear();
        UpdateStatus();
    }

    private void RenderPreview(ConfirmedRecord? record)
    {
        PreviewView.Clear();
        if (record is null) return;

        var byPos = new Dictionary<string, Student>();
        foreach (var p in record.Placements)
            byPos[p.Position.Key] = new Student
            {
                StudentNumber = p.StudentKey,
                Name = p.StudentName,
                Gender = p.Gender,
            };

        Student? Lookup(SeatPosition pos) =>
            byPos.TryGetValue(pos.Key, out var s) ? s : null;

        foreach (var sec in SeatGridBuilder.Build(record.Config, Lookup))
            PreviewView.Add(sec);
    }

    private void UpdateStatus() => Status = $"확정 기록 {Records.Count}건";
}
