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

/// <summary>제약 카드 1개(설정 + 드래그 우선순위). Owner로 부모 VM 데이터에 바인딩.</summary>
public sealed class ConstraintCardViewModel
{
    public required ConstraintsViewModel Owner { get; init; }
    public ConstraintKind Kind { get; init; }
    public string Title => Kind.ToKorean();
    public string Subtitle => Kind.Subtitle();
    public bool IsForbidden => Kind == ConstraintKind.ForbiddenPair;
    public bool IsRequired => Kind == ConstraintKind.RequiredPair;
    public bool IsFront => Kind == ConstraintKind.FrontRow;
    public bool IsGenderSeat => Kind == ConstraintKind.GenderSeat;
}

public partial class ConstraintsViewModel : ViewModelBase
{
    private readonly AppState _state;
    private SeatConstraints C => _state.Constraints;

    public ObservableCollection<Student> Students => _state.Roster;

    public ObservableCollection<PairRow> ForbiddenRows { get; } = new();
    public ObservableCollection<PairRow> RequiredRows { get; } = new();
    public ObservableCollection<FrontRow> FrontRows { get; } = new();
    public ObservableCollection<ConstraintCardViewModel> Cards { get; } = new();

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

    // ── 잠금(보안) ─────────────────────────────────────────────
    [ObservableProperty] private bool _isLocked = true;
    [ObservableProperty] private bool _forceSetup;
    [ObservableProperty] private bool _confirmingReset;
    [ObservableProperty] private string _pinInput = "";
    [ObservableProperty] private string _pinConfirm = "";
    [ObservableProperty] private string _lockMessage = "";

    public bool IsUnlocked => !IsLocked;
    public bool NeedsSetup => string.IsNullOrEmpty(_state.Security.ConstraintsPinHash);
    public bool ShowReset => IsLocked && ConfirmingReset;
    public bool ShowSetup => IsLocked && !ConfirmingReset && (NeedsSetup || ForceSetup);
    public bool ShowUnlock => IsLocked && !ConfirmingReset && !ShowSetup;

    private void NotifyLockStates()
    {
        OnPropertyChanged(nameof(IsUnlocked));
        OnPropertyChanged(nameof(ShowReset));
        OnPropertyChanged(nameof(ShowSetup));
        OnPropertyChanged(nameof(ShowUnlock));
    }

    partial void OnIsLockedChanged(bool value) => NotifyLockStates();
    partial void OnForceSetupChanged(bool value) => NotifyLockStates();
    partial void OnConfirmingResetChanged(bool value) => NotifyLockStates();

    /// <summary>탭을 떠날 때 호출 — 다시 잠그고 입력 초기화.</summary>
    public void Lock()
    {
        IsLocked = true;
        ForceSetup = false;
        ConfirmingReset = false;
        PinInput = "";
        PinConfirm = "";
        LockMessage = "";
    }

    [RelayCommand]
    private void ForgotPin() => ConfirmingReset = true;

    [RelayCommand]
    private void CancelReset()
    {
        ConfirmingReset = false;
        LockMessage = "";
    }

    [RelayCommand]
    private void ResetConstraints()
    {
        // 모든 제약 + PIN 제거 후 새 PIN 설정 단계로. (내용은 노출되지 않고 삭제만)
        C.ForbiddenPairs.Clear();
        C.RequiredPairs.Clear();
        C.FrontRowStudents.Clear();
        _state.Security.ConstraintsPinHash = null;
        _state.SaveConstraints();
        OnPropertyChanged(nameof(NeedsSetup));
        Rebuild();

        ConfirmingReset = false;
        ForceSetup = false;
        PinInput = PinConfirm = "";
        LockMessage = "제약을 초기화했습니다. 새 PIN을 설정하세요.";
    }

    [RelayCommand]
    private void SetPin()
    {
        if (PinInput.Length < 4) { LockMessage = "PIN은 4자리 이상이어야 합니다."; return; }
        if (PinInput != PinConfirm) { LockMessage = "두 PIN이 일치하지 않습니다."; return; }
        _state.Security.ConstraintsPinHash = PinHasher.Hash(PinInput);
        _state.SaveConstraints();
        OnPropertyChanged(nameof(NeedsSetup));
        ForceSetup = false;
        PinInput = PinConfirm = "";
        LockMessage = "";
        IsLocked = false;
    }

    [RelayCommand]
    private void Unlock()
    {
        if (PinHasher.Verify(PinInput, _state.Security.ConstraintsPinHash))
        {
            PinInput = "";
            LockMessage = "";
            IsLocked = false;
        }
        else
        {
            LockMessage = "PIN이 일치하지 않습니다.";
            PinInput = "";
        }
    }

    [RelayCommand]
    private void ChangePin()
    {
        ForceSetup = true;
        IsLocked = true;
        PinInput = PinConfirm = "";
        LockMessage = "새 PIN을 설정하세요.";
    }

    public ConstraintsViewModel(AppState state)
    {
        _state = state;
        _frontRowCount = C.FrontRowCount;
        _state.Roster.CollectionChanged += (_, _) => Rebuild();
        Rebuild();
        RebuildCards();
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

    private void RebuildCards()
    {
        C.Priority = C.ConstraintPriority(); // 제약 4종만, 누락분 보강해 영속 일관성 유지
        Cards.Clear();
        foreach (var k in C.Priority)
            Cards.Add(new ConstraintCardViewModel { Owner = this, Kind = k });
    }

    /// <summary>카드 드래그 재정렬: moved를 target 위치 앞에 끼워 넣는다.</summary>
    public void ReorderPriority(ConstraintKind moved, ConstraintKind target)
    {
        if (moved == target) return;
        if (!ConstraintKindInfo.ConstraintKinds.Contains(moved) ||
            !ConstraintKindInfo.ConstraintKinds.Contains(target)) return;

        var order = C.ConstraintPriority();
        order.Remove(moved);
        int idx = order.IndexOf(target);
        if (idx < 0) idx = order.Count;
        order.Insert(idx, moved);

        C.Priority = order;
        _state.SaveConstraints();
        RebuildCards();
    }

    private void UpdateStatus() =>
        Status = $"짝 금지 {ForbiddenRows.Count} · 짝 필수 {RequiredRows.Count} · 앞자리 {FrontRows.Count}";
}
