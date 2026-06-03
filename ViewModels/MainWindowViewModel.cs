using CommunityToolkit.Mvvm.ComponentModel;
using SeatShuffler.Services;

namespace SeatShuffler.ViewModels;

/// <summary>3탭(명단·배정·기록)을 호스팅하는 셸. 공유 AppState를 자식 VM에 주입.</summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppState _state;

    /// <summary>클립보드/파일선택용. View가 attach될 때 Owner(TopLevel)를 주입한다.</summary>
    public UiServices Ui { get; }

    public RosterViewModel Roster { get; }
    public ConstraintsViewModel Constraints { get; }
    public AssignmentViewModel Assignment { get; }
    public HistoryViewModel History { get; }
    public InfoViewModel Info { get; }

    [ObservableProperty] private int _selectedTabIndex;

    public MainWindowViewModel(AppState state)
    {
        _state = state;
        Ui = new UiServices();
        Roster = new RosterViewModel(state, Ui, Ui);
        Constraints = new ConstraintsViewModel(state);
        Assignment = new AssignmentViewModel(state, new SeatAssignmentService());
        History = new HistoryViewModel(state);
        Info = new InfoViewModel(state, Ui);
    }

    // 디자인타임용
    public MainWindowViewModel() : this(new AppState()) { }

    public void Save() => _state.Save();
}
