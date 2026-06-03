using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeatShuffler.Models;
using SeatShuffler.Services;

namespace SeatShuffler.ViewModels;

public partial class RosterViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly IClipboardService _clipboard;
    private readonly IDialogService _dialog;
    private readonly RosterImportService _import = new();

    public ObservableCollection<Student> Students => _state.Roster;

    /// <summary>성별 콤보 선택지(XAML x:Static 바인딩용).</summary>
    public static Gender[] GenderValues { get; } =
        { Gender.Male, Gender.Female, Gender.Unspecified };

    [ObservableProperty] private Student? _selectedStudent;
    [ObservableProperty] private bool _replaceOnImport;
    [ObservableProperty] private string _status = "";

    public RosterViewModel(AppState state, IClipboardService clipboard, IDialogService dialog)
    {
        _state = state;
        _clipboard = clipboard;
        _dialog = dialog;
        UpdateStatus();
    }

    // 디자인타임용
    public RosterViewModel() : this(new AppState(), new UiServices(), new UiServices()) { }

    [RelayCommand]
    private void AddRow()
    {
        var s = new Student();
        Students.Add(s);
        SelectedStudent = s;
        Save();
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedStudent is null) return;
        Students.Remove(SelectedStudent);
        Save();
    }

    [RelayCommand]
    private void ClearAll()
    {
        Students.Clear();
        Save();
    }

    [RelayCommand]
    private async Task PasteFromClipboardAsync()
    {
        var text = await _clipboard.GetTextAsync();
        Apply(_import.ParseDelimited(text));
    }

    [RelayCommand]
    private async Task ImportFileAsync()
    {
        var picked = await _dialog.PickSpreadsheetAsync();
        if (picked is null) return;

        await using var stream = picked.Stream;
        RosterImportResult result;
        if (picked.Name.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(stream);
            result = _import.ParseDelimited(await reader.ReadToEndAsync());
        }
        else
        {
            result = _import.ParseXlsx(stream);
        }
        Apply(result);
    }

    private void Apply(RosterImportResult result)
    {
        if (ReplaceOnImport && result.Students.Count > 0)
            Students.Clear();

        foreach (var s in result.Students)
            Students.Add(s);

        Save();

        var msg = $"{result.Students.Count}명 추가됨.";
        if (result.Warnings.Count > 0)
            msg += $" 경고 {result.Warnings.Count}건: " + string.Join(" / ", result.Warnings.Take(3));
        Status = msg;
    }

    public void Save()
    {
        _state.SaveRoster();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var m = Students.Count(s => s.Gender == Gender.Male);
        var f = Students.Count(s => s.Gender == Gender.Female);
        Status = $"총 {Students.Count}명 (남 {m} · 녀 {f})";
    }
}
