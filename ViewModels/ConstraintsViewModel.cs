using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeatShuffler.Models;
using SeatShuffler.Services;

namespace SeatShuffler.ViewModels;

/// <summary>짝 금지/필수 짝 행 표시용.</summary>
public sealed class PairRow
{
    public required StudentPair Pair { get; init; }
    public string Display { get; init; } = "";
}

/// <summary>앞자리 학생 행 표시용.</summary>
public sealed class FrontRow
{
    public string Key { get; init; } = "";
    public string Display { get; init; } = "";
}

public partial class ConstraintsViewModel : ViewModelBase
{
    private readonly AppState _state;
    private SeatConstraints C => _state.Constraints;

    public ObservableCollection<Student> Students => _state.Roster;

    public ObservableCollection<PairRow> ForbiddenRows { get; } = new();
    public ObservableCollection<PairRow> RequiredRows { get; } = new();
    public ObservableCollection<FrontRow> FrontRows { get; } = new();

    [ObservableProperty] private Student? _forbiddenA;
    [ObservableProperty] private Student? _forbiddenB;
    [ObservableProperty] private Student? _requiredA;
    [ObservableProperty] private Student? _requiredB;
    [ObservableProperty] private Student? _frontStudent;

    [ObservableProperty] private PairRow? _selectedForbidden;
    [ObservableProperty] private PairRow? _selectedRequired;
    [ObservableProperty] private FrontRow? _selectedFront;

    [ObservableProperty] private decimal _frontRowCount;
    [ObservableProperty] private string _status = "";

    public ConstraintsViewModel(AppState state)
    {
        _state = state;
        _frontRowCount = C.FrontRowCount;
        _state.Roster.CollectionChanged += (_, _) => Rebuild();
        Rebuild();
    }

    // 디자인타임용
    public ConstraintsViewModel() : this(new AppState()) { }

    partial void OnFrontRowCountChanged(decimal value)
    {
        C.FrontRowCount = (int)System.Math.Max(1, value);
        _state.SaveConstraints();
    }

    private string NameOf(string key)
    {
        var s = _state.Roster.FirstOrDefault(x => x.Key == key);
        return s is null ? $"{key} (명단에 없음)" : (string.IsNullOrWhiteSpace(s.StudentNumber)
            ? s.Name : $"{s.Name}({s.StudentNumber})");
    }

    private void Rebuild()
    {
        ForbiddenRows.Clear();
        foreach (var p in C.ForbiddenPairs)
            ForbiddenRows.Add(new PairRow { Pair = p, Display = $"{NameOf(p.A)}  ✕  {NameOf(p.B)}" });

        RequiredRows.Clear();
        foreach (var p in C.RequiredPairs)
            RequiredRows.Add(new PairRow { Pair = p, Display = $"{NameOf(p.A)}  ♥  {NameOf(p.B)}" });

        FrontRows.Clear();
        foreach (var k in C.FrontRowStudents)
            FrontRows.Add(new FrontRow { Key = k, Display = NameOf(k) });

        UpdateStatus();
    }

    private bool AddPair(System.Collections.Generic.List<StudentPair> list, Student? a, Student? b)
    {
        if (a is null || b is null) { Status = "두 학생을 모두 선택하세요."; return false; }
        if (a.Key == b.Key) { Status = "서로 다른 학생을 선택하세요."; return false; }
        var pair = new StudentPair { A = a.Key, B = b.Key };
        if (list.Any(x => x.Key == pair.Key)) { Status = "이미 등록된 짝입니다."; return false; }
        list.Add(pair);
        _state.SaveConstraints();
        return true;
    }

    [RelayCommand]
    private void AddForbidden()
    {
        if (AddPair(C.ForbiddenPairs, ForbiddenA, ForbiddenB)) { ForbiddenA = ForbiddenB = null; Rebuild(); }
    }

    [RelayCommand]
    private void RemoveForbidden()
    {
        if (SelectedForbidden is null) return;
        C.ForbiddenPairs.RemoveAll(p => p.Key == SelectedForbidden.Pair.Key);
        _state.SaveConstraints();
        Rebuild();
    }

    [RelayCommand]
    private void AddRequired()
    {
        if (AddPair(C.RequiredPairs, RequiredA, RequiredB)) { RequiredA = RequiredB = null; Rebuild(); }
    }

    [RelayCommand]
    private void RemoveRequired()
    {
        if (SelectedRequired is null) return;
        C.RequiredPairs.RemoveAll(p => p.Key == SelectedRequired.Pair.Key);
        _state.SaveConstraints();
        Rebuild();
    }

    [RelayCommand]
    private void AddFront()
    {
        if (FrontStudent is null) { Status = "학생을 선택하세요."; return; }
        if (C.FrontRowStudents.Contains(FrontStudent.Key)) { Status = "이미 등록된 학생입니다."; return; }
        C.FrontRowStudents.Add(FrontStudent.Key);
        _state.SaveConstraints();
        FrontStudent = null;
        Rebuild();
    }

    [RelayCommand]
    private void RemoveFront()
    {
        if (SelectedFront is null) return;
        C.FrontRowStudents.Remove(SelectedFront.Key);
        _state.SaveConstraints();
        Rebuild();
    }

    private void UpdateStatus() =>
        Status = $"짝 금지 {ForbiddenRows.Count} · 짝 필수 {RequiredRows.Count} · 앞자리 {FrontRows.Count}";
}
