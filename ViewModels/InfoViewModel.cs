using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeatShuffler.Services;

namespace SeatShuffler.ViewModels;

public partial class InfoViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly IFolderService _folders;

    public string AppTitle => "자리바꾸기 (Seat Shuffler)";
    public string AppDescription => "교실 학생 자리를 조건에 맞춰 배정하는 데스크톱 앱.";

    public string DataDir => AppPaths.DataDir;
    public string RosterFile => AppPaths.RosterFile;
    public string HistoryFile => AppPaths.HistoryFile;
    public string ConstraintsFile => AppPaths.ConstraintsFile;

    public string Counts => $"학생 {_state.Roster.Count}명 · 확정 기록 {_state.History.Count}건";

    public InfoViewModel(AppState state, IFolderService folders)
    {
        _state = state;
        _folders = folders;
        _state.Roster.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Counts));
        _state.History.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Counts));
    }

    // 디자인타임용
    public InfoViewModel() : this(new AppState(), new UiServices()) { }

    [RelayCommand]
    private async Task OpenDataFolderAsync() => await _folders.OpenFolderAsync(DataDir);
}
